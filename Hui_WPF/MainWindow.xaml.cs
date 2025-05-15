// MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hui_WPF.Models;
using Hui_WPF.ViewModels;
using Hui_WPF.Utils;
using Ookii.Dialogs.Wpf;
using Microsoft.Win32;
using System.Runtime.InteropServices;

#nullable enable

namespace Hui_WPF
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? realTimeTimer;
        private Brush? defaultTextBoxForeground; // Made nullable to handle CS8618
        private Brush placeholderForeground = Brushes.Gray;
        private bool isTextBoxFocused = false;

        private MainViewModel? ViewModel => DataContext as MainViewModel;

        public MainWindow()
        {
            InitializeComponent();

            LocalizationHelper.Initialize();

            if (txtFolderPath != null) // Check txtFolderPath before accessing its Foreground
            {
                defaultTextBoxForeground = txtFolderPath.Foreground;
            }
            else
            {
                // Fallback if txtFolderPath is somehow null at this point, though unlikely after InitializeComponent
                defaultTextBoxForeground = SystemColors.WindowTextBrush;
            }


            InitializeRealTimeTimer();

            var reporter = new UIReporter(
                Dispatcher,
                LogMessage,
                UpdateStatusLabel,
                UpdateProgressBar,
                UpdateCounts,
                UpdateActiveTasks,
                OpenFolderInExplorer,
                ShowSaveFileDialog,
                ShowBrowseDialog,
                ShowFolderBrowserDialog,
                ShowImageFileDialog,
                ShowMessageBoxWrapper, // Pass the synchronous wrapper
                ApplyLanguage,
                FindToolPath
            );

            DataContext = new MainViewModel(reporter);

            SetupLanguageComboBox();

            if (ViewModel?.SelectedLanguage != null)
            {
                LocalizationHelper.SetLanguage(ViewModel.SelectedLanguage.Code);
                ApplyLanguage();
            }
            else
            {
                LocalizationHelper.SetLanguage("zh");
                ApplyLanguage();
            }
            UpdateUIState();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {

            System.Diagnostics.Debug.WriteLine($"MainWindow Loaded: ViewModel.GlobalNameOrder = {ViewModel.GlobalNameOrder}");

            // Force Update Binding (might help with timing issues)
            var bindingExpression = cmbNameOrder.GetBindingExpression(ComboBox.SelectedValueProperty);
            bindingExpression?.UpdateTarget();

            // _isLoaded field removed as it was unused.
            if (ViewModel == null) return;

            if (navListBox.Items.Count > 0 && navListBox.SelectedIndex == -1)
            {
                for (int i = 0; i < navListBox.Items.Count; i++)
                {
                    if (navListBox.Items[i] is ListBoxItem lbi && lbi.IsEnabled)
                    {
                        navListBox.SelectedIndex = i;
                        break;
                    }
                }
            }
            await ViewModel.PerformToolCheckAsync();
            UpdateUIState();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (ViewModel?.IsProcessing ?? false)
            {
                if (ViewModel != null) LogMessage(LocalizationHelper.GetLocalizedString("CancelRequested"));
                ViewModel?.ExecuteCancelProcessing();
                e.Cancel = true;
            }
            else
            {
                ViewModel?.SaveSettings();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && (ViewModel?.IsProcessing ?? false))
            {
                if (ViewModel != null) LogMessage(LocalizationHelper.GetLocalizedString("CancelRequested"));
                ViewModel?.ExecuteCancelProcessing();
                e.Handled = true;
            }
        }

        private void Window_DragEnter(object sender, DragEventArgs e) => UpdateDragEffect(e);
        private void Window_DragOver(object sender, DragEventArgs e) => UpdateDragEffect(e);
        private void Window_Drop(object sender, DragEventArgs e) => HandleItemDrop(e);
        private void Panel_DragEnter(object sender, DragEventArgs e) => UpdateDragEffect(e);
        private void Panel_DragLeave(object sender, DragEventArgs e) { e.Handled = true; }
        private void Panel_DragDrop(object sender, DragEventArgs e) => HandleItemDrop(e);
        private void TextBox_DragEnter(object sender, DragEventArgs e) => UpdateDragEffect(e);
        private void TextBox_DragDrop(object sender, DragEventArgs e) => HandleItemDrop(e);

        private void UpdateDragEffect(DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && !(ViewModel?.IsProcessing ?? false) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }
        private void HandleItemDrop(DragEventArgs e)
        {
            if (ViewModel == null || (ViewModel?.IsProcessing ?? false)) return;
            if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0)
            {
                ViewModel.AddInputPaths(paths);
                e.Handled = true;
            }
        }

        private void TxtFolderPath_GotFocus(object sender, RoutedEventArgs e) { isTextBoxFocused = true; if (txtFolderPath != null && IsPlaceholderText(txtFolderPath.Text)) { txtFolderPath.Text = ""; txtFolderPath.Foreground = defaultTextBoxForeground; } }
        private void TxtFolderPath_LostFocus(object sender, RoutedEventArgs e) { isTextBoxFocused = false; if (txtFolderPath != null && string.IsNullOrWhiteSpace(txtFolderPath.Text)) { UpdateUIState(); } }
        private bool IsPlaceholderText(string t) => t == LocalizationHelper.GetLocalizedString("UnselectedState");
        private bool IsSummaryText(string t)
        {
            string selPrefix = LocalizationHelper.GetLocalizedString("Selected", "");
            int prefixLen = selPrefix.IndexOf('{');
            if (prefixLen < 0) prefixLen = selPrefix.Length;
            return t.StartsWith(selPrefix.Substring(0, prefixLen)) || t == LocalizationHelper.GetLocalizedString("InvalidItemsSelected");
        }

        public void LogMessage(string message)
        {
            Action action = () => {
                if (TbLog != null)
                {
                    string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                    const int MaxLogLength = 30000;
                    if (TbLog.Text.Length > MaxLogLength + 5000)
                    {
                        try
                        {
                            int startIndex = TbLog.Text.Length - MaxLogLength;
                            TbLog.Text = "-- Log Trimmed --" + Environment.NewLine + TbLog.Text.Substring(startIndex);
                        }
                        catch { try { TbLog.Clear(); TbLog.AppendText("-- Log Cleared (Catch) --\n"); } catch (Exception ex) { Debug.WriteLine($"FATAL: Could not clear log TextBox: {ex.Message}"); } }
                    }
                    TbLog.AppendText(logEntry);
                    TbLog.ScrollToEnd();
                }
            };
            if (TbLog?.Dispatcher != null && !TbLog.Dispatcher.CheckAccess()) TbLog.Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
            else if (TbLog?.Dispatcher != null) try { action(); } catch (Exception ex) { Debug.WriteLine($"Direct Log Err:{ex.Message}"); }
            else Debug.WriteLine($"Log fail (no dispatcher):{message}");
        }

        private void UpdateProgressBar(int value, int? max = null, bool indeterminate = false)
        {
            if (progressBar == null) return;
            Dispatcher.InvokeAsync(() => {
                if (max.HasValue && max.Value > 0) progressBar.Maximum = max.Value;
                else if (max.HasValue && max.Value <= 0) progressBar.Maximum = 1;
                progressBar.IsIndeterminate = indeterminate;
                if (!indeterminate) progressBar.Value = Math.Max(progressBar.Minimum, Math.Min((double)value, progressBar.Maximum));
            }, DispatcherPriority.Background);
        }

        private void UpdateStatusLabel(string text) => Dispatcher.InvokeAsync(() => { if (lblProcessStatus != null) lblProcessStatus.Text = text; }, DispatcherPriority.Background);

        private void UpdateCounts(int processed, int failed, int total)
        {
            Dispatcher.InvokeAsync(() => {
                if (lblImageCount != null) lblImageCount.Text = LocalizationHelper.GetLocalizedString("ProcessedCounts", processed, failed, total > 0 ? total : (processed + failed));
                if (lblProgressHint != null) lblProgressHint.Text = LocalizationHelper.GetLocalizedString("ProgressCounts", processed + failed, total > 0 ? total : (processed + failed));
            }, DispatcherPriority.Background);
        }

        private void UpdateActiveTasks(int count)
        {
            Dispatcher.InvokeAsync(() => {
                if (lblConcurrentTasks != null) lblConcurrentTasks.Text = LocalizationHelper.GetLocalizedString("StatusBar_Concurrent", count);
            }, DispatcherPriority.Background);
        }

        private void InitializeRealTimeTimer() { realTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) }; if (realTimeTimer != null) realTimeTimer.Tick += RealTimeTimer_Tick; realTimeTimer?.Start(); RealTimeTimer_Tick(null, EventArgs.Empty); }
        private void RealTimeTimer_Tick(object? sender, EventArgs e) { if (lblRealTimeClock != null) { lblRealTimeClock.Text = DateTime.Now.ToString("HH:mm:ss"); } }

        public void ApplyLanguage()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(ApplyLanguage); return; }
            if (ViewModel == null) return; // Check ViewModel null
            this.Title = LocalizationHelper.GetLocalizedString("WindowTitle");
            UpdateNavListBoxContent();
            ApplyToolTipsInternal();
            UpdateUIState();
        }

        private void UpdateNavListBoxContent()
        {
            if (navListBox == null) return;
            Dictionary<string, string> navItemKeys = new Dictionary<string, string>()
             {
                 {"创建目录", "Nav_CreateDirectory"}, {"直接命名", "Nav_DirectRename"},
                 {"文件处理", "Nav_FileProcessing"}, {"EXIF 移除", "Nav_ExifRemove"},
                 {"EXIF 写入", "Nav_ExifWrite"}, {"媒体生成", "Nav_MediaGeneration"},
                 {"生成视频", "Nav_GenerateVideo"}, {"生成连拍", "Nav_GenerateBurst"},
                 {"生成动画", "Nav_GenerateAnimation"},
             };
            foreach (var item in navListBox.Items)
            {
                if (item is ListBoxItem lbi && lbi.Content is string content)
                {
                    string originalContent = lbi.Tag as string ?? content;
                    lbi.Tag = originalContent;
                    if (navItemKeys.TryGetValue(originalContent, out string? key))
                    { lbi.Content = LocalizationHelper.GetLocalizedString(key, originalContent); }
                }
            }
        }

        private void ApplyToolTipsInternal() { Action<DependencyObject?, string> setTip = (e, k) => { if (e != null) try { ToolTipService.SetToolTip(e, LocalizationHelper.GetLocalizedString(k)); } catch { } }; setTip(txtFolderPath, "Tooltip_FolderPath"); setTip(btnBrowseFolder, "Tooltip_BrowseFolder"); setTip(cmbLanguage, "Tooltip_LanguageSelector"); setTip(navListBox, "Tooltip_NavigationPane"); setTip(btnStartAction, "Tooltip_StartProcessing"); setTip(btnCancelAction, "Tooltip_CancelAction"); setTip(SourceSelectionDropZone, "Tooltip_DragDropPanel"); setTip(TbLog, "Tooltip_Log"); }

        private void SetupLanguageComboBox()
        {
            if (cmbLanguage == null || ViewModel == null) return;
            cmbLanguage.DisplayMemberPath = "DisplayName";
            cmbLanguage.SelectedValuePath = "Code";
        }

        private void UpdateUIState()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(UpdateUIState); return; }
            if (ViewModel == null || txtFolderPath == null) return; // Check txtFolderPath for null
            bool proc = ViewModel.IsProcessing;
            if (!isTextBoxFocused && !proc)
            {
                string vmSummary = ViewModel.SelectedInputPathSummary;
                bool isPh = IsPlaceholderText(vmSummary) || IsSummaryText(vmSummary);
                if (txtFolderPath.Text != vmSummary) { txtFolderPath.Text = vmSummary; }
                txtFolderPath.Foreground = isPh ? placeholderForeground : defaultTextBoxForeground;
            }
            else if (isTextBoxFocused) { txtFolderPath.Foreground = defaultTextBoxForeground; }
        }

        private async Task<string?> ShowSaveFileDialog(string title, string? initialDirectory, string? defaultFileName, string filter, string defaultExtension, CancellationToken token)
        {
            return await FileDialogHelper.ShowSaveFileDialogAsync(this, title, initialDirectory, defaultFileName, filter, defaultExtension, token);
        }

        private async Task<string[]?> ShowBrowseDialog(string title, string? initialDirectory, CancellationToken token)
        {
            return await FileDialogHelper.ShowBrowseDialogAsync(this, title, initialDirectory, token);
        }

        private async Task<string?> ShowFolderBrowserDialog(string title, string? initialDirectory, CancellationToken token)
        {
            return await FileDialogHelper.ShowFolderBrowserDialogAsync(this, title, initialDirectory, token);
        }

        private async Task<string[]?> ShowImageFileDialog(string title, string? initialDirectory, bool multiselect, CancellationToken token)
        {
            return await FileDialogHelper.ShowImageFileDialogAsync(this, title, initialDirectory, multiselect, token);
        }

        private MessageBoxResult ShowMessageBoxWrapper(string message, string? title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            return MessageBox.Show(this, message, title ?? string.Empty, buttons, icon);
        }

        public void OpenFolderInExplorer(string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                if (Directory.Exists(full))
                {
                    LogMessage(string.Format(LocalizationHelper.GetLocalizedString("OpenFolderComplete"), full));
                    Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{full}\"", UseShellExecute = true });
                }
                else
                {
                    string? fallbackDir = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(fallbackDir) && Directory.Exists(fallbackDir))
                    {
                        LogMessage(string.Format(LocalizationHelper.GetLocalizedString("OpenFolderFallback"), full, fallbackDir));
                        Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{fallbackDir}\"", UseShellExecute = true });
                    }
                    else
                    {
                        LogMessage(string.Format(LocalizationHelper.GetLocalizedString("OpenFolderFallbackFailed"), full));
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage(string.Format(LocalizationHelper.GetLocalizedString("OpenFolderFailed"), path, ex.Message));
            }
        }

        internal string? FindToolPath(string executableName)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string toolSubfolder;
            string specificBinPath = "";
            switch (executableName.ToLowerInvariant())
            {
                case "exiftool.exe": toolSubfolder = "exiftool"; break;
                case "magick.exe": case "convert.exe": toolSubfolder = "ImageMagick"; break;
                case "ffmpeg.exe": case "ffprobe.exe": toolSubfolder = "ffmpeg"; specificBinPath = Path.Combine(baseDir, toolSubfolder, "bin", executableName); break;
                default: toolSubfolder = ""; break;
            }
            if (!string.IsNullOrEmpty(specificBinPath) && File.Exists(specificBinPath)) { return specificBinPath; }
            if (!string.IsNullOrEmpty(toolSubfolder)) { string subfolderPath = Path.Combine(baseDir, toolSubfolder, executableName); if (File.Exists(subfolderPath)) { return subfolderPath; } }
            string basePath = Path.Combine(baseDir, executableName); if (File.Exists(basePath)) { return basePath; }
            string? pathVar = Environment.GetEnvironmentVariable("PATH");
            if (pathVar != null)
            {
                foreach (string pathDir in pathVar.Split(Path.PathSeparator))
                {
                    try { string pathFilePath = Path.Combine(pathDir.Trim(), executableName); if (File.Exists(pathFilePath)) { return pathFilePath; } } catch { }
                }
            }
            LogMessage($"Warning: Tool '{executableName}' not found in expected subdirectories ('{toolSubfolder}', '{Path.Combine(toolSubfolder, "bin")}') or system PATH.");
            return null;
        }

        internal bool IsAccessException(IOException ex)
        {
            const int E_ACCESSDENIED = unchecked((int)0x80070005);
            const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
            const int ERROR_LOCK_VIOLATION = unchecked((int)0x80070021);
            if (ex.InnerException is Win32Exception win32Ex)
            {
                return win32Ex.NativeErrorCode == 5 || win32Ex.NativeErrorCode == 32 || win32Ex.NativeErrorCode == 33;
            }
            return ex.HResult == E_ACCESSDENIED ||
                   ex.HResult == ERROR_SHARING_VIOLATION ||
                   ex.HResult == ERROR_LOCK_VIOLATION ||
                   ex.Message.Contains("being used", StringComparison.OrdinalIgnoreCase) ||
                   ex.Message.Contains("Access to the path", StringComparison.OrdinalIgnoreCase);
        }
    }
}