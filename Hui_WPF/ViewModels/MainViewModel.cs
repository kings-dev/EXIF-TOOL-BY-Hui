// ViewModels/MainViewModel.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Hui_WPF.Core;
using Hui_WPF.Models;
using Hui_WPF.Utils;
using Hui_WPF.Views; // Not strictly needed here if using DataTemplates in App.xaml
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System.Runtime.InteropServices;
using Hui_WPF.utils;
using System.Windows.Controls;

namespace Hui_WPF.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly SettingsManager _settingsManager;
        private readonly IUIReporter _reporter;
        private readonly FileNamer _fileNamer;

        private static readonly int MaxConcurrentProcesses = Math.Max(1, Environment.ProcessorCount / 2);
        public readonly SemaphoreSlim processSemaphore = new SemaphoreSlim(MaxConcurrentProcesses, MaxConcurrentProcesses);
        public int _activeTasksCount = 0;

        private CancellationTokenSource? _processingCtsInternal;
        public CancellationToken ProcessingCtsToken => _processingCtsInternal?.Token ?? CancellationToken.None;

        private bool _isProcessing = false;
        public bool IsProcessing
        {
            get => _isProcessing;
            private set { if (SetProperty(ref _isProcessing, value)) CommandManager.InvalidateRequerySuggested(); }
        }

        private string _statusText = "";
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private double _progressValue = 0;
        public double ProgressValue { get => _progressValue; set => SetProperty(ref _progressValue, value); }

        private bool _isProgressIndeterminate = false;
        public bool IsProgressIndeterminate { get => _isProgressIndeterminate; set => SetProperty(ref _isProgressIndeterminate, value); }

        private string _logContent = "";
        public string LogContent { get => _logContent; set => SetProperty(ref _logContent, value); }

        public ObservableCollection<string> InputPaths { get; } = new ObservableCollection<string>();
        private string _selectedInputPathSummary = "";
        public string SelectedInputPathSummary { get => _selectedInputPathSummary; set => SetProperty(ref _selectedInputPathSummary, value); }

        private ViewModelBase? _currentView;
        public ViewModelBase? CurrentView
        {
            get => _currentView;
            set { if (SetProperty(ref _currentView, value)) UpdateTaskViewModelSettings(); }
        }
        private string _selectedNavItem = "创建目录"; // Default to Create Directory
        public string SelectedNavItem
        {
            get => _selectedNavItem;
            set
            {
                // 直接使用传入的字符串值
                if (SetProperty(ref _selectedNavItem, string.IsNullOrWhiteSpace(value) ? "创建目录" : value))
                {
                    // 只在有效字符串值改变时加载内容
                    LoadContentForSelection(_selectedNavItem);
                }
            }
        }

        private string _countsSummaryText = "";
        public string CountsSummaryText { get => _countsSummaryText; set => SetProperty(ref _countsSummaryText, value); }

        private string _progressSummaryText = "";
        public string ProgressSummaryText { get => _progressSummaryText; set => SetProperty(ref _progressSummaryText, value); }

        private string _startTimeText = "";
        public string StartTimeText { get => _startTimeText; set => SetProperty(ref _startTimeText, value); }

        private string _elapsedTimeText = "";
        public string ElapsedTimeText { get => _elapsedTimeText; set => SetProperty(ref _elapsedTimeText, value); }

        private string _endTimeText = "";
        public string EndTimeText { get => _endTimeText; set => SetProperty(ref _endTimeText, value); }

        private string _totalTimeText = "";
        public string TotalTimeText { get => _totalTimeText; set => SetProperty(ref _totalTimeText, value); }

        private string _activeTasksText = "";
        public string ActiveTasksText { get => _activeTasksText; set => SetProperty(ref _activeTasksText, value); }

        private bool _actionButtonsVisible = true;
        public bool ActionButtonsVisible { get => _actionButtonsVisible; set => SetProperty(ref _actionButtonsVisible, value); }

        private bool _sourceSelectionVisible = true;
        public bool SourceSelectionVisible { get => _sourceSelectionVisible; set => SetProperty(ref _sourceSelectionVisible, value); }

        private NamingOptions _globalNamingOptions = NamingOptionsDefaults.Default;
        public NamingOptions GlobalNamingOptions
        {
            get => _globalNamingOptions;
            set { if (SetProperty(ref _globalNamingOptions, value)) { UpdateTaskViewModelSettings(); if (value != null) value.PropertyChanged += GlobalNamingOptions_PropertyChanged; } }
        }

        private PathOptions _customPathOptions = PathOptionsDefaults.Default;
        public PathOptions CustomPathOptions
        {
            get => _customPathOptions;
            set { if (SetProperty(ref _customPathOptions, value)) { UpdateTaskViewModelSettings(); if (value != null) value.PropertyChanged += CustomPathOptions_PropertyChanged; } }
        }

        private bool _generalEnableBackup = true;
        public bool GeneralEnableBackup
        {
            get => _generalEnableBackup;
            set { if (SetProperty(ref _generalEnableBackup, value)) UpdateTaskViewModelSettings(); }
        }

        public ObservableCollection<string> NameOrderOptions { get; } = new ObservableCollection<string>
        {
            // 缩写优先级顺序：夹/父/子/名，然后时间戳和计数器
            "夹/父/子/名+时间戳+计数器"
        };

        private const string DefaultGlobalNameOrder = "夹/父/子/名+时间戳+计数器"; // 默认全局顺序
        private string _globalNameOrder = DefaultGlobalNameOrder; // 初始化全局命名顺序
        public string GlobalNameOrder
        {
            get => _globalNameOrder;
            set
            {
                if (SetProperty(ref _globalNameOrder, string.IsNullOrWhiteSpace(value) ? "夹/父/子/名+时间戳+计数器" : value))
                {
                    UpdateTaskViewModelSettings();
                }
            }
        }

        private string _outputImageFormat = "JPEG";
        public string OutputImageFormat
        {
            get => _outputImageFormat;
            set { if (SetProperty(ref _outputImageFormat, value ?? "JPEG")) { OnPropertyChanged(nameof(IsJpegOutput)); UpdateTaskViewModelSettings(); } }
        }

        private int _jpegQuality = 90;
        public int JpegQuality
        {
            get => _jpegQuality;
            set { if (SetProperty(ref _jpegQuality, value)) UpdateTaskViewModelSettings(); }
        }

        private int _originalFileActionIndex = 0;
        public int OriginalFileActionIndex
        {
            get => _originalFileActionIndex;
            set { if (SetProperty(ref _originalFileActionIndex, value)) UpdateTaskViewModelSettings(); }
        }

        private string _selectedExifToolTag = "ExifTool";
        public string SelectedExifToolTag
        {
            get => _selectedExifToolTag;
            set { if (SetProperty(ref _selectedExifToolTag, value ?? "ExifTool")) UpdateTaskViewModelSettings(); }
        }

        public bool IsJpegOutput => OutputImageFormat?.Equals("JPEG", StringComparison.OrdinalIgnoreCase) ?? false;

        private bool _useTimestampSubfolderForMedia = true;
        public bool UseTimestampSubfolderForMedia { get => _useTimestampSubfolderForMedia; set => SetProperty(ref _useTimestampSubfolderForMedia, value); }

        private bool _overwriteOutput = true;
        public bool OverwriteOutput { get => _overwriteOutput; set => SetProperty(ref _overwriteOutput, value); }

        public ObservableCollection<LanguageItem> AvailableLanguages { get; }

        private LanguageItem? _selectedLanguage;
        public LanguageItem? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value) && value != null)
                {
                    LocalizationHelper.SetLanguage(value.Code);
                    _reporter.LogMessage(LocalizationHelper.GetLocalizedString("LanguageChanged"));
                    _reporter.ApplyLocalization();
                }
            }
        }

        public RelayCommand StartProcessingCommand { get; }
        public RelayCommand CancelProcessingCommand { get; }
        public RelayCommand BrowseFolderCommand { get; }
        public RelayCommand SelectFolderCommand { get; }
        public RelayCommand SelectImagesCommand { get; }
        public RelayCommand ClearLogCommand { get; }
        public RelayCommand SaveLogCommand { get; }
        public RelayCommand ResetParamsCommand { get; }
        public RelayCommand RestoreDefaultsCommand { get; }
        public RelayCommand<string> BrowseCustomPathCommand { get; }

        private DateTime _startTime;
        private Stopwatch _processStopwatch = new Stopwatch();

        public MainViewModel(IUIReporter reporter)
        {
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
            _settingsManager = new SettingsManager();
            _fileNamer = new FileNamer();
            AvailableLanguages = new ObservableCollection<LanguageItem>(LocalizationHelper.GetAvailableLanguages());
            InputPaths.CollectionChanged += (s, e) => UpdateInputPathSummary();

            StartProcessingCommand = new RelayCommand(async () => await ExecuteStartProcessingAsync(), () => CanStartProcessingInternal());
            CancelProcessingCommand = new RelayCommand(ExecuteCancelProcessing, () => IsProcessing);
            BrowseFolderCommand = new RelayCommand(async () => await ExecuteBrowseFolderAsync(), () => !IsProcessing);
            SelectFolderCommand = new RelayCommand(async () => await ExecuteSelectFolderAsync(), () => !IsProcessing);
            SelectImagesCommand = new RelayCommand(async () => await ExecuteSelectImagesAsync(), () => !IsProcessing);
            ClearLogCommand = new RelayCommand(ExecuteClearLog);
            SaveLogCommand = new RelayCommand(async () => await ExecuteSaveLogAsync());
            ResetParamsCommand = new RelayCommand(ExecuteResetParams, () => !IsProcessing);
            RestoreDefaultsCommand = new RelayCommand(ExecuteRestoreDefaults, () => !IsProcessing);
            BrowseCustomPathCommand = new RelayCommand<string>(async (pathType) => await ExecuteBrowseCustomPathAsync(pathType), (pathType) => !IsProcessing);

            LoadSettings(); // This will now correctly handle default for GlobalNameOrder
            if (GlobalNamingOptions != null) GlobalNamingOptions.PropertyChanged += GlobalNamingOptions_PropertyChanged;
            if (CustomPathOptions != null) CustomPathOptions.PropertyChanged += CustomPathOptions_PropertyChanged;

            StatusText = LocalizationHelper.GetLocalizedString("ProcessStatusLabelInitial");
            CountsSummaryText = LocalizationHelper.GetLocalizedString("ProcessedCounts", 0, 0, 0);
            ProgressSummaryText = LocalizationHelper.GetLocalizedString("ProgressCounts", 0, 0);
            StartTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Start")} -";
            ElapsedTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Elapsed")} -";
            EndTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_End")} -";
            TotalTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Total")} -";
            ActiveTasksText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Concurrent")} 0";
            LoadContentForSelection(SelectedNavItem);

            if (string.IsNullOrWhiteSpace(GlobalNameOrder))
                GlobalNameOrder = DefaultGlobalNameOrder;
        }

        private void GlobalNamingOptions_PropertyChanged(object? sender, PropertyChangedEventArgs e) { UpdateTaskViewModelSettings(); CommandManager.InvalidateRequerySuggested(); }
        private void CustomPathOptions_PropertyChanged(object? sender, PropertyChangedEventArgs e) { UpdateTaskViewModelSettings(); CommandManager.InvalidateRequerySuggested(); }

        public bool CanStartProcessing => CanStartProcessingInternal();
        public bool CanCancelProcessing => IsProcessing;
        private bool CanStartProcessingInternal()
        {
            if (IsProcessing) return false;
            bool hasSelectedItems = InputPaths.Any();
            if (CurrentView is CreateDirectoryViewModel createDirVm) { return !string.IsNullOrWhiteSpace(createDirVm.BasePath) && IsValidPath(createDirVm.BasePath); }
            else if (CurrentView is GenerateBurstViewModel) { return hasSelectedItems && InputPaths.Count == 1 && Directory.Exists(InputPaths.FirstOrDefault() ?? ""); }
            else if (CurrentView is IProcessingTaskViewModel) { return hasSelectedItems; }
            return false;
        }

        private void LoadSettings()
        {
            var loaded = _settingsManager.LoadApplicationSettings();
            GlobalNamingOptions = new NamingOptions(loaded.Naming);
            CustomPathOptions = new PathOptions(loaded.Paths);
            GeneralEnableBackup = Properties.Settings.Default.UserEnableBackup;
            OutputImageFormat = Properties.Settings.Default.UserOutputImageFormat ?? "JPEG";
            JpegQuality = Properties.Settings.Default.UserJpegQuality;
            OriginalFileActionIndex = Properties.Settings.Default.UserOriginalFileAction;

            string loadedGlobalNameOrder = Properties.Settings.Default.GlobalNameOrder;
            if (string.IsNullOrWhiteSpace(loadedGlobalNameOrder))
            {
                Properties.Settings.Default.GlobalNameOrder = DefaultGlobalNameOrder;
                Properties.Settings.Default.Save();
                loadedGlobalNameOrder = DefaultGlobalNameOrder;
            }
            GlobalNameOrder = loadedGlobalNameOrder;

            UseTimestampSubfolderForMedia = Properties.Settings.Default.UserUseTimestampSubfolderForMedia;
            OverwriteOutput = Properties.Settings.Default.UserOverwriteOutput;
            SelectedExifToolTag = loaded.ExifToolTag;
            SelectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code.Equals(loaded.Language, StringComparison.OrdinalIgnoreCase));
            if (SelectedLanguage == null && AvailableLanguages.Any()) SelectedLanguage = AvailableLanguages.First();

            OnPropertyChanged(nameof(GlobalNamingOptions)); OnPropertyChanged(nameof(CustomPathOptions)); OnPropertyChanged(nameof(GeneralEnableBackup)); OnPropertyChanged(nameof(GlobalNameOrder)); OnPropertyChanged(nameof(OutputImageFormat)); OnPropertyChanged(nameof(JpegQuality)); OnPropertyChanged(nameof(OriginalFileActionIndex)); OnPropertyChanged(nameof(SelectedExifToolTag)); OnPropertyChanged(nameof(UseTimestampSubfolderForMedia)); OnPropertyChanged(nameof(OverwriteOutput));
        }

        public void SaveSettings()
        {
            _settingsManager.SaveApplicationSettings(SelectedLanguage?.Code ?? "zh", GlobalNamingOptions, CustomPathOptions, SelectedExifToolTag);
            Properties.Settings.Default.UserEnableBackup = GeneralEnableBackup;
            Properties.Settings.Default.UserOutputImageFormat = OutputImageFormat;
            Properties.Settings.Default.UserJpegQuality = JpegQuality;
            Properties.Settings.Default.UserOriginalFileAction = OriginalFileActionIndex;
            Properties.Settings.Default.GlobalNameOrder = GlobalNameOrder; // Save current value
            Properties.Settings.Default.UserUseTimestampSubfolderForMedia = UseTimestampSubfolderForMedia;
            Properties.Settings.Default.UserOverwriteOutput = OverwriteOutput;
            Properties.Settings.Default.Save();
            _reporter.LogMessage(LocalizationHelper.GetLocalizedString("Application settings saved."));
        }

        public void AddInputPaths(IEnumerable<string> paths)
        {
            if (IsProcessing) return;
            _reporter.LogMessage(LocalizationHelper.GetLocalizedString("SelectionStartedLog") + (InputPaths.Any() ? " (Appending)" : " (Replacing)"));
            InputPaths.Clear(); int addedCount = 0;
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    string norm = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (Directory.Exists(norm) || File.Exists(norm))
                    {
                        if (!InputPaths.Contains(norm, StringComparer.OrdinalIgnoreCase)) { InputPaths.Add(norm); addedCount++; }
                        else { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("IgnoringDuplicateItem", norm)); }
                    }
                    else { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("IgnoringInvalidPath", norm)); }
                }
                catch (Exception ex) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorAddingPath", path ?? "null", ex.Message)); }
            }
            if (addedCount > 0) _reporter.LogMessage(LocalizationHelper.GetLocalizedString("SelectionCompleteLog", addedCount));
            else { if (!InputPaths.Any()) _reporter.LogMessage(LocalizationHelper.GetLocalizedString("NoValidItemsAddedLog")); else _reporter.LogMessage(LocalizationHelper.GetLocalizedString("DropNoNewItems")); }
            UpdateInputPathSummary();
        }

        public void ClearInputPaths() { InputPaths.Clear(); UpdateInputPathSummary(); _reporter.LogMessage(LocalizationHelper.GetLocalizedString("InputCleared.")); }

        private void UpdateInputPathSummary()
        {
            if (!InputPaths.Any()) { SelectedInputPathSummary = LocalizationHelper.GetLocalizedString("UnselectedState"); }
            else
            {
                int fCnt = 0, dCnt = 0;
                foreach (var p in InputPaths) { try { if (File.Exists(p)) fCnt++; else if (Directory.Exists(p)) dCnt++; } catch { } }
                var parts = new List<string>();
                if (dCnt > 0) parts.Add($"{dCnt} {LocalizationHelper.GetLocalizedString("Folders")}");
                if (fCnt > 0) parts.Add($"{(dCnt > 0 ? LocalizationHelper.GetLocalizedString("And", " 和 ") : "")}{fCnt} {LocalizationHelper.GetLocalizedString("Files")}");
                if (parts.Any())
                {
                    if (InputPaths.Count == 1) { try { SelectedInputPathSummary = Path.GetFullPath(InputPaths.First()); } catch { SelectedInputPathSummary = InputPaths.First(); } }
                    else { SelectedInputPathSummary = LocalizationHelper.GetLocalizedString("Selected", string.Join("", parts)); }
                }
                else { SelectedInputPathSummary = LocalizationHelper.GetLocalizedString("InvalidItemsSelected"); }
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void LoadContentForSelection(string? viewName)
        {
            ActionButtonsVisible = true; 
            //SourceSelectionVisible = false;
            ViewModelBase? newView = null;
            switch (viewName)
            {
                case "创建目录": newView = GetOrCreateViewModel<CreateDirectoryViewModel>(); 
                    //SourceSelectionVisible = false; 
                    break;
                case "直接命名": newView = GetOrCreateViewModel<DirectRenameViewModel>(); break;
                case "EXIF 移除": newView = GetOrCreateViewModel<ExifRemoveViewModel>(); break;
                case "EXIF 写入": newView = GetOrCreateViewModel<ExifWriteViewModel>(); break;
                case "生成视频": newView = GetOrCreateViewModel<GenerateVideoViewModel>(); break;
                case "生成连拍": newView = GetOrCreateViewModel<GenerateBurstViewModel>(); break;
                case "生成动画": newView = GetOrCreateViewModel<GenerateAnimationViewModel>(); break;
                case "文件处理": case "媒体生成": newView = null; ActionButtonsVisible = false; 
                    //SourceSelectionVisible = false;
                    break;
                default: newView = new PlaceholderViewModel($"View for: {viewName}"); ActionButtonsVisible = false; 
                    //SourceSelectionVisible = false; 
                    break;
            }
            CurrentView = newView;
        }

        private void UpdateTaskViewModelSettings()
        {
            if (CurrentView is IProcessingTaskViewModel taskViewModel)
            {
                taskViewModel.LoadSettings(GlobalNamingOptions, CustomPathOptions, GeneralEnableBackup, OriginalFileActionIndex, OutputImageFormat, JpegQuality, SelectedExifToolTag);
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private Dictionary<Type, ViewModelBase> _viewModelCache = new Dictionary<Type, ViewModelBase>();
        private T GetOrCreateViewModel<T>() where T : ViewModelBase
        {
            if (_viewModelCache.TryGetValue(typeof(T), out var vm)) { return (T)vm; }
            else
            {
                T newVm;
                if (typeof(T) == typeof(CreateDirectoryViewModel)) newVm = (T)(object)new CreateDirectoryViewModel(_reporter, this);
                else if (typeof(T) == typeof(DirectRenameViewModel)) newVm = (T)(object)new DirectRenameViewModel(_reporter, this);
                else if (typeof(T) == typeof(ExifRemoveViewModel)) newVm = (T)(object)new ExifRemoveViewModel(_reporter, this);
                else if (typeof(T) == typeof(ExifWriteViewModel)) newVm = (T)(object)new ExifWriteViewModel(_reporter, this);
                else if (typeof(T) == typeof(GenerateVideoViewModel)) newVm = (T)(object)new GenerateVideoViewModel(_reporter, this);
                else if (typeof(T) == typeof(GenerateBurstViewModel)) newVm = (T)(object)new GenerateBurstViewModel(_reporter, this);
                else if (typeof(T) == typeof(GenerateAnimationViewModel)) newVm = (T)(object)new GenerateAnimationViewModel(_reporter, this);
                else newVm = Activator.CreateInstance<T>();
                _viewModelCache[typeof(T)] = newVm;
                return newVm;
            }
        }

        private async Task ExecuteBrowseFolderAsync()
        {
            string? initialDir = GetInitialPathFromInput(SelectedInputPathSummary);
            string[]? selectedPaths = await _reporter.ShowBrowseDialogAsync(LocalizationHelper.GetLocalizedString("SelectFilesAndFoldersTitle"), initialDir, ProcessingCtsToken);
            if (selectedPaths != null && selectedPaths.Length > 0) AddInputPaths(selectedPaths);
            else _reporter.LogMessage(LocalizationHelper.GetLocalizedString("SelectionCancelled"));
        }

        private async Task ExecuteSelectFolderAsync()
        {
            string? initialDir = GetInitialPathFromInput(SelectedInputPathSummary);
            string? selectedFolder = await _reporter.ShowFolderBrowserDialogAsync(LocalizationHelper.GetLocalizedString("SelectFolder"), initialDir, ProcessingCtsToken);
            if (selectedFolder != null) AddInputPaths(new[] { selectedFolder });
            else _reporter.LogMessage(LocalizationHelper.GetLocalizedString("SelectionCancelled"));
        }

        private async Task ExecuteSelectImagesAsync()
        {
            string? initialDir = GetInitialPathFromInput(SelectedInputPathSummary);
            string[]? selectedFiles = await _reporter.ShowImageFileDialogAsync(LocalizationHelper.GetLocalizedString("SelectOneOrMoreImages"), initialDir, true, ProcessingCtsToken);
            if (selectedFiles != null && selectedFiles.Length > 0) AddInputPaths(selectedFiles);
            else _reporter.LogMessage(LocalizationHelper.GetLocalizedString("SelectionCancelled"));
        }

        private async Task ExecuteBrowseCustomPathAsync(string? pathType)
        {
            if (string.IsNullOrWhiteSpace(pathType)) return;
            string titleKey = ""; string currentPath = "";
            switch (pathType)
            {
                case "BackupPath": titleKey = "SelectCustomBackupFolderTitle"; currentPath = CustomPathOptions.CustomBackupPath; break;
                case "ImageOutputPath": titleKey = "SelectCustomImageOutputPathLabel"; currentPath = CustomPathOptions.CustomImageOutputPath; break;
                case "VideoOutputPath": titleKey = "SelectCustomVideoOutputPathLabel"; currentPath = CustomPathOptions.CustomVideoOutputPath; break;
                default: return;
            }
            string? selectedFolder = await _reporter.ShowFolderBrowserDialogAsync(LocalizationHelper.GetLocalizedString(titleKey), currentPath, CancellationToken.None);
            if (selectedFolder != null)
            {
                switch (pathType)
                {
                    case "BackupPath": CustomPathOptions.CustomBackupPath = selectedFolder; break;
                    case "ImageOutputPath": CustomPathOptions.CustomImageOutputPath = selectedFolder; break;
                    case "VideoOutputPath": CustomPathOptions.CustomVideoOutputPath = selectedFolder; break;
                }
            }
        }

        private async Task ExecuteStartProcessingAsync()
        {
            if (!CanStartProcessingInternal())
            {
                _reporter.LogMessage("Start processing validation failed.");
                if (CurrentView is CreateDirectoryViewModel cdv && string.IsNullOrWhiteSpace(cdv.BasePath))
                { await _reporter.ShowMessageAsync(LocalizationHelper.GetLocalizedString("ErrorValidBasePathRequired"), LocalizationHelper.GetLocalizedString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning, CancellationToken.None); }
                else if (CurrentView is GenerateBurstViewModel && !(InputPaths.Count == 1 && Directory.Exists(InputPaths.FirstOrDefault() ?? "")))
                { await _reporter.ShowMessageAsync(LocalizationHelper.GetLocalizedString("BurstModeWarning"), LocalizationHelper.GetLocalizedString("Tip"), MessageBoxButton.OK, MessageBoxImage.Warning, CancellationToken.None); }
                return;
            }
            _reporter.LogMessage("Start Processing button clicked.");
            SaveSettings();
            IsProcessing = true;
            _processingCtsInternal?.Dispose();
            _processingCtsInternal = new CancellationTokenSource();
            var token = _processingCtsInternal.Token;
            _reporter.UpdateProgressBar(0, indeterminate: true);
            _reporter.UpdateStatusLabel(LocalizationHelper.GetLocalizedString("ProcessingReady"));
            _reporter.UpdateCounts(0, 0, 0); _reporter.UpdateActiveTasks(0);
            _startTime = DateTime.Now; _processStopwatch.Restart();
            StartTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Start")} {_startTime:HH:mm:ss}";
            EndTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_End")} ...";
            ElapsedTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Elapsed")} 00:00:00.0";
            TotalTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Total")} ...";
            bool wasCancelled = false;
            try
            {
                _reporter.UpdateProgressBar(0, indeterminate: false);
                if (CurrentView is IProcessingTaskViewModel taskViewModel)
                {
                    await taskViewModel.ExecuteAsync(InputPaths.ToList(), token, _reporter);
                }
                else { _reporter.LogMessage("No executable task ViewModel selected."); _reporter.UpdateStatusLabel("Idle"); }
            }
            catch (OperationCanceledException) { wasCancelled = true; _reporter.LogMessage(LocalizationHelper.GetLocalizedString("ProcessingCancelled")); }
            catch (Exception ex)
            {
                string errorMsgKey = "FatalProcessingError";
                if (CurrentView is DirectRenameViewModel) errorMsgKey = "DirectRename_FatalError";
                else if (CurrentView is GenerateVideoViewModel || CurrentView is GenerateBurstViewModel || CurrentView is GenerateAnimationViewModel) errorMsgKey = "ErrorGeneratingZoompan";
                string trace = ex.StackTrace ?? LocalizationHelper.GetLocalizedString("NoStackTrace");
                _reporter.LogMessage(LocalizationHelper.GetLocalizedString(errorMsgKey, ex.Message, trace));
                await _reporter.ShowMessageAsync(LocalizationHelper.GetLocalizedString(errorMsgKey, ex.Message, ""), LocalizationHelper.GetLocalizedString("ProcessingErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None);
            }
            finally
            {
                _processStopwatch.Stop(); DateTime endTime = DateTime.Now; TimeSpan totalDuration = _processStopwatch.Elapsed; string totalFormatted = FormatTimeSpan(totalDuration);
                IsProcessing = false; _activeTasksCount = 0; _reporter.UpdateActiveTasks(0);
                EndTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_End")} {endTime:HH:mm:ss}";
                TotalTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Total")} {totalFormatted}";
                ElapsedTimeText = $"{LocalizationHelper.GetLocalizedString("StatusBar_Elapsed")} {totalFormatted}";
                string finalStatus;
                int finalProcessed = _reporter.ProcessedCount; int finalFailed = _reporter.FailedCount; int finalTotal = _reporter.TotalCount;
                if (wasCancelled) finalStatus = LocalizationHelper.GetLocalizedString("ProcessingCancelled");
                else if (CurrentView is DirectRenameViewModel) finalStatus = (finalTotal == 0 && finalProcessed == 0 && finalFailed == 0) ? LocalizationHelper.GetLocalizedString("DirectRename_NothingSelected") : LocalizationHelper.GetLocalizedString("DirectRename_FinishedLog", finalProcessed, finalFailed);
                else if (CurrentView is GenerateVideoViewModel || CurrentView is GenerateBurstViewModel || CurrentView is GenerateAnimationViewModel) finalStatus = LocalizationHelper.GetLocalizedString("ZoompanGenerationComplete", finalProcessed, finalFailed);
                else finalStatus = (finalTotal == 0 && finalProcessed == 0 && finalFailed == 0) ? LocalizationHelper.GetLocalizedString("NoImagesFound") : LocalizationHelper.GetLocalizedString("ProcessingCompleted", finalProcessed, finalFailed);
                _reporter.UpdateStatusLabel(finalStatus); _reporter.LogMessage(finalStatus);
                if (!wasCancelled && finalProcessed > 0)
                {
                    string? firstSuccessfullyProcessedTopLevelPath = FindFirstOutputPath(InputPaths.ToList(), CustomPathOptions, GeneralEnableBackup);
                    if (!string.IsNullOrEmpty(firstSuccessfullyProcessedTopLevelPath)) _reporter.OpenFolderInExplorer(firstSuccessfullyProcessedTopLevelPath);
                    else _reporter.LogMessage("Could not determine a relevant output folder to open.");
                }
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public void ExecuteCancelProcessing()
        {
            _reporter.LogMessage(LocalizationHelper.GetLocalizedString("CancelRequested") + "...");
            _reporter.UpdateStatusLabel(LocalizationHelper.GetLocalizedString("CancelRequested") + "...");
            _processingCtsInternal?.Cancel();
            CommandManager.InvalidateRequerySuggested();
        }

        private void ExecuteClearLog() { LogContent = ""; _reporter.LogMessage(LocalizationHelper.GetLocalizedString("ClearLogMessage")); }

        private async Task ExecuteSaveLogAsync()
        {
            string defaultName = $"ExifDog_Log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            string? savePath = await _reporter.ShowSaveFileDialogAsync(
                LocalizationHelper.GetLocalizedString("SaveLog"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                defaultName, $"{LocalizationHelper.GetLocalizedString("TextFile")}(*.txt)|*.txt|{LocalizationHelper.GetLocalizedString("AllFiles")}(*.*)|*.*",
                ".txt", ProcessingCtsToken);
            if (savePath != null)
            {
                try { File.WriteAllText(savePath, LogContent, Encoding.UTF8); _reporter.LogMessage(LocalizationHelper.GetLocalizedString("LogSaved", savePath)); }
                catch (Exception ex) { string msg = LocalizationHelper.GetLocalizedString("ErrorSavingLog", ex.Message); _reporter.LogMessage(msg); await _reporter.ShowMessageAsync(msg, LocalizationHelper.GetLocalizedString("SaveErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None); }
            }
        }

        private void ExecuteResetParams()
        {
            _reporter.LogMessage("Reset Parameters button clicked.");
            LoadSettings();
            _reporter.ShowMessage(LocalizationHelper.GetLocalizedString("ParamsResetMsg"), LocalizationHelper.GetLocalizedString("ParamsResetTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            UpdateTaskViewModelSettings();
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// 恢复应用默认参数，不使用上一次保存的设置
        /// </summary>
        private void ExecuteRestoreDefaults()
        {
            _reporter.LogMessage("Restore Defaults button clicked.");
            // 恢复全局命名默认
            GlobalNamingOptions = NamingOptionsDefaults.Default;
            // 恢复自定义路径默认
            CustomPathOptions = PathOptionsDefaults.Default;
            // 恢复其它通用设置
            GeneralEnableBackup = true;
            OutputImageFormat = "JPEG";
            JpegQuality = 90;
            OriginalFileActionIndex = 0;
            GlobalNameOrder = DefaultGlobalNameOrder;
            UseTimestampSubfolderForMedia = true;
            OverwriteOutput = true;
            SelectedExifToolTag = "ExifTool";
            // 更新界面和MVVM设置
            UpdateTaskViewModelSettings();
            CommandManager.InvalidateRequerySuggested();
            _reporter.ShowMessage(LocalizationHelper.GetLocalizedString("DefaultsResetMsg"), LocalizationHelper.GetLocalizedString("DefaultsResetTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string FormatTimeSpan(TimeSpan ts) => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 100}";

        public async Task PerformToolCheckAsync()
        {
            _reporter.UpdateProgressBar(0, indeterminate: true);
            _reporter.UpdateStatusLabel(LocalizationHelper.GetLocalizedString("CheckingTools"));
            try
            {
                await Task.Delay(100);
                CancellationToken tokenForTools = ProcessingCtsToken;
                await ZipsHelper.EnsureAllToolsReady(
                    new Progress<int>(p => _reporter.UpdateProgressBar(p, indeterminate: false)),
                    new Progress<string>(msg => _reporter.LogMessage(msg)),
                    tokenForTools
                );
                _reporter.LogMessage(LocalizationHelper.GetLocalizedString("AllToolsReadyComplete"));
                _reporter.UpdateProgressBar(100, indeterminate: false);
            }
            catch (OperationCanceledException)
            {
                _reporter.LogMessage(LocalizationHelper.GetLocalizedString("ToolCheckCancelled"));
                _reporter.UpdateStatusLabel(LocalizationHelper.GetLocalizedString("ToolCheckCancelled"));
                _reporter.UpdateProgressBar(0, indeterminate: false);
            }
            catch (Exception ex)
            {
                string errorMsg = LocalizationHelper.GetLocalizedString("ToolCheckError", ex.Message);
                _reporter.LogMessage(errorMsg);
                _reporter.UpdateStatusLabel(errorMsg);
                _reporter.UpdateProgressBar(0, indeterminate: false);
                await _reporter.ShowMessageAsync(errorMsg, LocalizationHelper.GetLocalizedString("ToolCheckErrorTitle", "Tool Check Error"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None);
            }
        }

        public string GetInitialPathFromInput(string? currentInput)
        {
            if (string.IsNullOrWhiteSpace(currentInput) || IsPlaceholderText(currentInput) || IsSummaryText(currentInput)) return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            try { string fullPath = Path.GetFullPath(currentInput.Trim()); if (Directory.Exists(fullPath)) return fullPath; if (File.Exists(fullPath)) return Path.GetDirectoryName(fullPath); } catch { }
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        public bool IsPlaceholderText(string t) => t == LocalizationHelper.GetLocalizedString("UnselectedState");
        public bool IsSummaryText(string t)
        {
            string selPrefix = LocalizationHelper.GetLocalizedString("Selected", "");
            int prefixLen = selPrefix.IndexOf('{');
            if (prefixLen < 0) prefixLen = selPrefix.Length;
            return t.StartsWith(selPrefix.Substring(0, prefixLen)) || t == LocalizationHelper.GetLocalizedString("InvalidItemsSelected");
        }
        public bool IsValidPath(string path)
        {
            try { if (string.IsNullOrWhiteSpace(path)) return false; Path.GetFullPath(path); return true; } catch { return false; }
        }
        public bool IsAccessException(IOException ex)
        {
            const int E_ACCESSDENIED = unchecked((int)0x80070005); const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020); const int ERROR_LOCK_VIOLATION = unchecked((int)0x80070021);
            if (ex.InnerException is Win32Exception win32Ex) { return win32Ex.NativeErrorCode == 5 || win32Ex.NativeErrorCode == 32 || win32Ex.NativeErrorCode == 33; }
            return ex.HResult == E_ACCESSDENIED || ex.HResult == ERROR_SHARING_VIOLATION || ex.HResult == ERROR_LOCK_VIOLATION || ex.Message.Contains("being used", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("Access to the path", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string?> GetValidatedCustomPathAsync(string? path, bool createDirectoryIfNeeded, string pathType, IUIReporter reporter)
        {
            if (string.IsNullOrWhiteSpace(path)) { reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "CustomPathEmptyWarning" : "CustomOutputPathEmptyWarning")); return null; }
            try
            {
                if (!Path.IsPathRooted(path)) { reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "CustomPathInvalid" : "CustomOutputPathInvalid", path) + " Reason: Path not absolute."); return null; }
                if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) { reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "CustomPathInvalid" : "CustomOutputPathInvalid", path) + " Reason: Contains invalid chars."); return null; }
                string fullPath = Path.GetFullPath(path);
                if (Directory.Exists(fullPath))
                {
                    reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "CustomPathValid" : "CustomOutputPathValid", fullPath));
                    return fullPath;
                }
                if (createDirectoryIfNeeded)
                {
                    try
                    {
                        reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "CustomPathCreateAttempt" : "CustomOutputPathCreateAttempt", fullPath));
                        await System.Threading.Tasks.Task.Run(() => Directory.CreateDirectory(fullPath));
                        reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "CustomPathValid" : "CustomOutputPathValid", fullPath) + " (Created)");
                        return fullPath;
                    }
                    catch (Exception createEx) { reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "ErrorCreatingCustomBackupDir" : "ErrorCreatingCustomOutputDir", fullPath, createEx.Message)); return null; }
                }
                else { reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "CustomPathInvalid" : "CustomOutputPathInvalid", fullPath) + " Reason: Does not exist."); return null; }
            }
            catch (Exception ex) { reporter.LogMessage(LocalizationHelper.GetLocalizedString(pathType == "Backup" ? "CustomPathVerifyError" : "CustomOutputPathVerifyError", path ?? "null", ex.Message)); return null; }
        }

        public string GetValidatedTimestampString(string format, DateTime time)
        {
            string defaultFormat = NamingOptionsDefaults.TimestampFormat;
            string validatedFormat = defaultFormat; string testOutput = "";
            if (!string.IsNullOrWhiteSpace(format))
            {
                format = format.Trim();
                try
                {
                    testOutput = time.ToString(format);
                    char[] invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray();
                    bool invalidOutputContent = string.IsNullOrWhiteSpace(testOutput) || testOutput.IndexOfAny(invalidChars) >= 0 || testOutput.EndsWith(".", StringComparison.Ordinal) || testOutput.EndsWith(" ", StringComparison.Ordinal);
                    if (invalidOutputContent) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnTimestampFormatProducesInvalid", format, testOutput)); validatedFormat = defaultFormat; try { testOutput = time.ToString(validatedFormat); } catch { testOutput = ""; } }
                    else { validatedFormat = format; }
                }
                catch (FormatException) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnInvalidTimestampFormat", format, defaultFormat)); validatedFormat = defaultFormat; try { testOutput = time.ToString(validatedFormat); } catch { testOutput = ""; } }
                catch (ArgumentException argEx) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnProblematicTimestampFormat", format, argEx.Message, defaultFormat)); validatedFormat = defaultFormat; try { testOutput = time.ToString(validatedFormat); } catch { testOutput = ""; } }
                catch (Exception ex) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorGeneratingTimestamp", format, ex.Message) + " Using default."); validatedFormat = defaultFormat; try { testOutput = time.ToString(validatedFormat); } catch { testOutput = ""; } }
            }
            else { validatedFormat = defaultFormat; try { testOutput = time.ToString(validatedFormat); } catch { testOutput = ""; } }
            if (string.IsNullOrWhiteSpace(testOutput) || testOutput.IndexOfAny(Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct().ToArray()) >= 0 || testOutput.EndsWith(".", StringComparison.Ordinal) || testOutput.EndsWith(" ", StringComparison.Ordinal))
            {
                if (validatedFormat != defaultFormat) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnTimestampFormatProducesInvalidFinal", validatedFormat, testOutput, defaultFormat)); validatedFormat = defaultFormat; try { testOutput = time.ToString(validatedFormat); } catch { testOutput = ""; } }
                else if (string.IsNullOrWhiteSpace(testOutput)) { try { testOutput = time.ToString(CultureInfo.InvariantCulture); } catch { testOutput = ""; } }
            }
            return testOutput;
        }

        public string GetValidatedTimestampFormat(string format, bool forFolder = false)
        {
            string defaultFormat = NamingOptionsDefaults.TimestampFormat; string validatedFormat = defaultFormat;
            if (!string.IsNullOrWhiteSpace(format))
            {
                format = format.Trim();
                try
                {
                    string testOutput = DateTime.Now.ToString(format); bool invalidChars = false;
                    char[] checkedChars = forFolder ? Path.GetInvalidPathChars() : Path.GetInvalidFileNameChars();
                    if (format.IndexOfAny(checkedChars) >= 0) invalidChars = true;
                    else if (format.EndsWith(".") || format.EndsWith(" ")) invalidChars = true;
                    else if (string.IsNullOrWhiteSpace(testOutput)) invalidChars = true;
                    else if (testOutput.IndexOfAny(checkedChars) >= 0) invalidChars = true;
                    if (invalidChars) _reporter.LogMessage(LocalizationHelper.GetLocalizedString(forFolder ? "WarnTimestampFormatInvalidFolder" : "WarnTimestampFormatInvalidChars", format)); else validatedFormat = format;
                }
                catch (FormatException) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnInvalidTimestampFormat", format, defaultFormat)); }
                catch (ArgumentException argEx) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnProblematicTimestampFormat", format, argEx.Message, defaultFormat)); }
            }
            return string.IsNullOrWhiteSpace(validatedFormat) ? defaultFormat : validatedFormat;
        }

        public string GetValidatedCounterFormat(string format)
        {
            string defaultFormat = NamingOptionsDefaults.CounterFormat; 
            string validatedFormat = defaultFormat;
            if (!string.IsNullOrWhiteSpace(format))
            {
                format = format.Trim();
                if (format == "中文")
                {
                    validatedFormat = format;
                }
                else
                {
                    try { _ = 1.ToString(format); validatedFormat = format; }
                    catch (FormatException) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnInvalidCounterFormat", format, defaultFormat)); }
                    catch (Exception ex) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorFormattingCounter", 1, format, ex.Message) + " Using default."); }
                }
            }
            return string.IsNullOrWhiteSpace(validatedFormat) ? defaultFormat : validatedFormat;
        }

        public string GetValidatedCounterString(string format, int counterValue)
        {
            string validatedFormat = GetValidatedCounterFormat(format);
            try 
            { 
                return counterValue.ToString(); 
            }
            catch { return counterValue.ToString(NamingOptionsDefaults.CounterFormat); }
        }


        public int GetValidatedStartValue(int startValue)
        {
            int defaultStart = NamingOptionsDefaults.CounterStartValue;
            if (startValue >= 0) { return startValue; }
            else { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnInvalidCounterStartValue", startValue, defaultStart)); return defaultStart; }
        }

        public string ConstructNameFromGlobalSettings(int currentCounter, DateTime currentTime, string? defaultBaseNameForFolderComponent = "示例目录", string? originalFileNameWithoutExtension = null, string? explicitFolderNameOverride = null, bool isSubDir = false)
        {
            string currentGlobalNameOrder = string.IsNullOrWhiteSpace(this.GlobalNameOrder) ? "夹/父/子/名+时间戳+计数器" : this.GlobalNameOrder;
            string[] order = currentGlobalNameOrder.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            string separator = GlobalNamingOptions.UseSeparator ? (string.IsNullOrWhiteSpace(GlobalNamingOptions.Separator) ? "_" : GlobalNamingOptions.Separator) : "";

            // 获取文件夹相关的名称组件
            string folderText = GlobalNamingOptions.IncludeFolder ? (GlobalNamingOptions.FolderText?.Trim() ?? "") : "";
            string parentDirText = GlobalNamingOptions.IncludeParentDir ? (GlobalNamingOptions.ParentDirText?.Trim() ?? "") : "";
            string subDirText = GlobalNamingOptions.IncludeSubDir ? (GlobalNamingOptions.SubDirText?.Trim() ?? "") : "";

            // 优先处理文件名独占逻辑
            if (GlobalNamingOptions.IncludeFileName && !string.IsNullOrWhiteSpace(originalFileNameWithoutExtension))
            {
                var nameParts = new List<string> { DirectoryRule.CleanPathSegment(originalFileNameWithoutExtension) };
                if (GlobalNamingOptions.IncludeTimestamp)
                {
                    string timestamp = currentTime.ToString(GlobalNamingOptions.TimestampFormat);
                    nameParts.Add(timestamp);
                }
                if (GlobalNamingOptions.IncludeCounter)
                {
                    string counter = currentCounter.ToString(GlobalNamingOptions.CounterFormat);
                    nameParts.Add(counter);
                }
                return string.Join(separator, nameParts);
            }

            // 根据优先级选择主名称
            string mainName = null;
            if (isSubDir)
            {
                // 子目录优先级：子目录 > 文件夹 > 父目录 > 文件名
                if (!string.IsNullOrWhiteSpace(subDirText))
                    mainName = subDirText;
                else if (!string.IsNullOrWhiteSpace(folderText))
                    mainName = folderText;
                else if (!string.IsNullOrWhiteSpace(parentDirText))
                    mainName = parentDirText;
                else if (!string.IsNullOrWhiteSpace(originalFileNameWithoutExtension))
                    mainName = originalFileNameWithoutExtension;
                else
                    mainName = defaultBaseNameForFolderComponent ?? "";
            }
            else
            {
                // 主目录优先级：文件夹 > 父目录 > 子目录 > 文件名
                if (!string.IsNullOrWhiteSpace(folderText))
                    mainName = folderText;
                else if (!string.IsNullOrWhiteSpace(parentDirText))
                    mainName = parentDirText;
                else if (!string.IsNullOrWhiteSpace(subDirText))
                    mainName = subDirText;
                else if (!string.IsNullOrWhiteSpace(originalFileNameWithoutExtension))
                    mainName = originalFileNameWithoutExtension;
                else
                    mainName = defaultBaseNameForFolderComponent ?? "";
            }

            // 如果提供了显式覆盖名称，则使用它
            if (!string.IsNullOrWhiteSpace(explicitFolderNameOverride))
            {
                mainName = explicitFolderNameOverride;
            }

            // 构建最终名称
            var namePartsFull = new List<string>();
            foreach (string componentKeyInOrder in order)
            {
                string keyTrimmed = componentKeyInOrder.Trim();
                if (keyTrimmed.Contains("/")) // 处理"夹/父/子/名"
                {
                    if (!string.IsNullOrWhiteSpace(mainName))
                        namePartsFull.Add(DirectoryRule.CleanPathSegment(mainName));
                }
                else if (keyTrimmed.Equals("文件夹", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(mainName))
                        namePartsFull.Add(DirectoryRule.CleanPathSegment(mainName));
                }
                else if (keyTrimmed.Equals("时间戳", StringComparison.OrdinalIgnoreCase) && GlobalNamingOptions.IncludeTimestamp)
                {
                    string timestamp = currentTime.ToString(GlobalNamingOptions.TimestampFormat);
                    namePartsFull.Add(timestamp);
                }
                else if (keyTrimmed.Equals("计数器", StringComparison.OrdinalIgnoreCase) && GlobalNamingOptions.IncludeCounter)
                {
                    string counter = currentCounter.ToString(GlobalNamingOptions.CounterFormat);
                    namePartsFull.Add(counter);
                }
            }

            return string.Join(separator, namePartsFull);
        }

        public string DetermineBaseOutputDirectory(string? customVideoOutputPathValidated, bool useTimestampSubfolder, bool enableTimestampNaming, string timestampFolderFormat)
        {
            string baseDir;
            if (customVideoOutputPathValidated != null) { baseDir = customVideoOutputPathValidated; }
            else
            {
                baseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GlobalNamingOptions.OutputSubfolder ?? "Output");
                if (useTimestampSubfolder && enableTimestampNaming)
                {
                    try
                    {
                        string timestampStringForDir = GetValidatedTimestampString(timestampFolderFormat, DateTime.Now);
                        string cleanedTimestampDirName = DirectoryRule.CleanPathSegment(timestampStringForDir);
                        if (string.IsNullOrWhiteSpace(cleanedTimestampDirName) || cleanedTimestampDirName.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || cleanedTimestampDirName.EndsWith(".") || cleanedTimestampDirName.EndsWith(" "))
                        {
                            _reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarnTimestampFormatInvalidFolder", timestampFolderFormat, cleanedTimestampDirName));
                            timestampStringForDir = GetValidatedTimestampString(NamingOptionsDefaults.TimestampFormat, DateTime.Now);
                            cleanedTimestampDirName = DirectoryRule.CleanPathSegment(timestampStringForDir);
                        }
                        baseDir = Path.Combine(baseDir, cleanedTimestampDirName);
                    }
                    catch (Exception ex)
                    {
                        _reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorGeneratingTimestampFolder", timestampFolderFormat, ex.Message));
                        string fallbackTimestamp = GetValidatedTimestampString(NamingOptionsDefaults.TimestampFormat, DateTime.Now);
                        string cleanedFallbackTimestamp = DirectoryRule.CleanPathSegment(fallbackTimestamp);
                        baseDir = Path.Combine(baseDir, cleanedFallbackTimestamp);
                    }
                }
            }
            if (!Directory.Exists(baseDir))
            {
                try { Directory.CreateDirectory(baseDir); _reporter.LogMessage(LocalizationHelper.GetLocalizedString("CreatedOutputDir", baseDir)); }
                catch (Exception dirEx) { _reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorCreatingCustomOutputDir", baseDir, dirEx.Message)); throw; }
            }
            return baseDir;
        }

        public async Task<bool> PerformPreProcessingBackupAsync(List<string> originalInputPaths, NamingOptions globalNaming, PathOptions customPaths, bool enableBackupFlag, CancellationToken token, IUIReporter reporter, Dictionary<string, string> outSourceToBackupPathMap)
        {
            reporter.LogMessage(LocalizationHelper.GetLocalizedString("StartingBackup"));
            outSourceToBackupPathMap.Clear();
            bool overallSuccess = true;
            try
            {
                Dictionary<string, SourceType> uniqueValidSources = GetUniqueValidSources(originalInputPaths, reporter);
                if (!uniqueValidSources.Any()) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("NoSourcesForBackup")); return true; }
                string backupRootNameBase = _fileNamer.CleanFileName(globalNaming.Prefix ?? LocalizationHelper.GetLocalizedString("BackupDefaultPrefix"));
                if (globalNaming.IncludeTimestamp)
                {
                    string ts = GetValidatedTimestampString(globalNaming.TimestampFormat, DateTime.Now);
                    if (!string.IsNullOrEmpty(ts)) backupRootNameBase = $"{backupRootNameBase}_{ts}";
                }
                string finalBackupFolderName = $"{backupRootNameBase}_Backup";
                bool useSingleFolderRenameLogic = false; bool useMultiFolderSameParentLogic = false; bool useFallbackLogic = false;
                string? singleFolderToRename = null; List<string>? multiFoldersToMove = null; string? commonParentForMulti = null;
                string targetBackupPath = "";

                if (customPaths.UseCustomBackupPath && !string.IsNullOrWhiteSpace(customPaths.CustomBackupPath))
                {
                    string? validatedCustomBackupPath = await GetValidatedCustomPathAsync(customPaths.CustomBackupPath, true, "Backup", reporter);
                    if (string.IsNullOrWhiteSpace(validatedCustomBackupPath)) { useFallbackLogic = true; targetBackupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backup", finalBackupFolderName); }
                    else { targetBackupPath = Path.Combine(validatedCustomBackupPath, finalBackupFolderName); useFallbackLogic = true; }
                }
                else
                {
                    bool allAreFolders = uniqueValidSources.Values.All(type => type == SourceType.Directory);
                    if (uniqueValidSources.Count == 1 && allAreFolders)
                    {
                        useSingleFolderRenameLogic = true; singleFolderToRename = uniqueValidSources.Keys.First();
                        string? parentDir = Path.GetDirectoryName(singleFolderToRename);
                        if (parentDir == null) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupCannotGetParent", singleFolderToRename)); return false; }
                        targetBackupPath = Path.Combine(parentDir, finalBackupFolderName);
                        reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupStrategySingleRename", singleFolderToRename, targetBackupPath));
                    }
                    else if (uniqueValidSources.Count > 1 && allAreFolders)
                    {
                        commonParentForMulti = FindCommonParentDirectory(uniqueValidSources.Keys);
                        if (commonParentForMulti != null)
                        {
                            useMultiFolderSameParentLogic = true; multiFoldersToMove = uniqueValidSources.Keys.ToList();
                            targetBackupPath = Path.Combine(commonParentForMulti, finalBackupFolderName);
                            reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupStrategyMultiFolderContainer", commonParentForMulti, targetBackupPath));
                        }
                        else { useFallbackLogic = true; reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupStrategyFallback") + " Reason: Multiple folders, different parents."); targetBackupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backup", finalBackupFolderName); }
                    }
                    else { useFallbackLogic = true; reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupStrategyFallback") + " Reason: Mixed files/folders or only files."); targetBackupPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backup", finalBackupFolderName); }
                }

                int suffix = 1; string tempTargetBackupPath = targetBackupPath;
                while (Directory.Exists(tempTargetBackupPath) || File.Exists(tempTargetBackupPath)) { tempTargetBackupPath = $"{targetBackupPath}({suffix++})"; if (suffix > 100) { reporter.LogMessage("Error: Could not find unique backup folder name."); return false; } }
                targetBackupPath = tempTargetBackupPath;

                if (useSingleFolderRenameLogic && singleFolderToRename != null)
                {
                    try { string? parentOfTarget = Path.GetDirectoryName(targetBackupPath); if (parentOfTarget != null && !Directory.Exists(parentOfTarget)) await System.Threading.Tasks.Task.Run(() => Directory.CreateDirectory(parentOfTarget), token); reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupAttemptRename", singleFolderToRename, targetBackupPath)); await System.Threading.Tasks.Task.Run(() => Directory.Move(singleFolderToRename, targetBackupPath), token); outSourceToBackupPathMap[singleFolderToRename] = targetBackupPath; reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupRenameSuccess")); }
                    catch (Exception ex) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupRenameError", singleFolderToRename, targetBackupPath, ex.Message)); overallSuccess = false; }
                }
                else if (useMultiFolderSameParentLogic && multiFoldersToMove != null && commonParentForMulti != null)
                {
                    try
                    {
                        string? parentOfTarget = Path.GetDirectoryName(targetBackupPath); if (parentOfTarget != null && !Directory.Exists(parentOfTarget)) await System.Threading.Tasks.Task.Run(() => Directory.CreateDirectory(parentOfTarget), token);
                        reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupCreateContainer", targetBackupPath)); await System.Threading.Tasks.Task.Run(() => Directory.CreateDirectory(targetBackupPath), token);
                        foreach (string folderToMove in multiFoldersToMove)
                        {
                            token.ThrowIfCancellationRequested(); string sourceFolderName = Path.GetFileName(folderToMove); string destinationInContainer = Path.Combine(targetBackupPath, sourceFolderName);
                            if (Directory.Exists(destinationInContainer) || File.Exists(destinationInContainer)) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupItemExistsInContainer", sourceFolderName, targetBackupPath)); outSourceToBackupPathMap[folderToMove] = destinationInContainer; continue; }
                            reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupMoveIntoContainer", folderToMove, targetBackupPath)); await System.Threading.Tasks.Task.Run(() => Directory.Move(folderToMove, destinationInContainer), token); outSourceToBackupPathMap[folderToMove] = destinationInContainer;
                        }
                        reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupMultiMoveComplete"));
                    }
                    catch (Exception ex) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupMultiMoveError", targetBackupPath, ex.Message)); overallSuccess = false; }
                }
                else if (useFallbackLogic)
                {
                    reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupMoveToCustom", targetBackupPath));
                    if (!Directory.Exists(targetBackupPath)) { try { await System.Threading.Tasks.Task.Run(() => Directory.CreateDirectory(targetBackupPath), token); } catch (Exception createEx) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorCreatingCustomOutputDir", targetBackupPath, createEx.Message)); overallSuccess = false; } }
                    if (overallSuccess)
                    {
                        foreach (var kvp in uniqueValidSources)
                        {
                            token.ThrowIfCancellationRequested(); string sourcePath = kvp.Key; SourceType sourceType = kvp.Value; string sourceName = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); string finalMoveTargetPath = Path.Combine(targetBackupPath, sourceName);
                            if (Directory.Exists(finalMoveTargetPath) || File.Exists(finalMoveTargetPath)) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupItemExistsInCustom", sourceName, targetBackupPath)); outSourceToBackupPathMap[sourcePath] = finalMoveTargetPath; continue; }
                            try
                            {
                                reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupMoveItemToCustom", sourcePath, finalMoveTargetPath));
                                if (sourceType == SourceType.Directory) { if (!Directory.Exists(sourcePath)) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("SourceNotFoundForMove", "directory", sourcePath)); overallSuccess = false; continue; } await System.Threading.Tasks.Task.Run(() => Directory.Move(sourcePath, finalMoveTargetPath), token); }
                                else { if (!File.Exists(sourcePath)) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("SourceNotFoundForMove", "file", sourcePath)); overallSuccess = false; continue; } string? targetDir = Path.GetDirectoryName(finalMoveTargetPath); if (targetDir != null && !Directory.Exists(targetDir)) await System.Threading.Tasks.Task.Run(() => Directory.CreateDirectory(targetDir), token); await System.Threading.Tasks.Task.Run(() => File.Move(sourcePath, finalMoveTargetPath), token); }
                                outSourceToBackupPathMap[sourcePath] = finalMoveTargetPath;
                            }
                            catch (IOException ioEx) when (IsAccessException(ioEx)) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("AccessDeniedMoveBackup", sourcePath, ioEx.Message)); overallSuccess = false; }
                            catch (Exception ex) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorMoveBackupGeneral", sourcePath, finalMoveTargetPath, ex.Message)); overallSuccess = false; }
                            if (!overallSuccess) break;
                        }
                        if (overallSuccess) reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupMoveCustomComplete"));
                    }
                }
                else { reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupErrorNoStrategy")); overallSuccess = false; }
            }
            catch (OperationCanceledException) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupCancelled")); throw; }
            catch (Exception ex) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupFatalError", ex.Message)); overallSuccess = false; }
            if (!overallSuccess) reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupFailedAbort"));
            return overallSuccess;
        }

        public async Task<List<Tuple<string, string>>> CollectAllUniqueFilesAndTheirOriginalPathsAsync(
            List<string> originalInputPaths,
            bool processFromBackup,
            Dictionary<string, string> sourceToBackupPathMap,
            CancellationToken token,
            IUIReporter reporter)
        {
            reporter.LogMessage(LocalizationHelper.GetLocalizedString("CollectingFiles"));
            reporter.UpdateStatusLabel(LocalizationHelper.GetLocalizedString("CollectingFiles"));
            await System.Threading.Tasks.Task.Delay(100, token);

            var pathsToScanDetails = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (processFromBackup)
            {
                reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupProcessingFromBackup"));
                foreach (string originalInput in originalInputPaths)
                {
                    if (sourceToBackupPathMap.TryGetValue(originalInput, out string? backupPath) && !string.IsNullOrEmpty(backupPath))
                    {
                        if (Directory.Exists(backupPath) || File.Exists(backupPath))
                            pathsToScanDetails[originalInput] = backupPath;
                        else
                            reporter.LogMessage(_reporter.GetLocalizedString("BackupMapEntryNotFound", originalInput) + " (Path does not exist)");
                    }
                    else
                    {
                        reporter.LogMessage(_reporter.GetLocalizedString("BackupMapEntryNotFound", originalInput));
                    }
                }
            }
            else
            {
                reporter.LogMessage(LocalizationHelper.GetLocalizedString("BackupProcessingOriginals"));
                foreach (string originalInput in originalInputPaths)
                {
                    if (Directory.Exists(originalInput) || File.Exists(originalInput))
                        pathsToScanDetails[originalInput] = originalInput;
                    else
                        reporter.LogMessage(LocalizationHelper.GetLocalizedString("IgnoringInvalidPath", originalInput));
                }
            }

            if (!pathsToScanDetails.Any())
            {
                reporter.LogMessage(LocalizationHelper.GetLocalizedString("NoImagesFound"));
                reporter.UpdateProgressBar(0, null, false);
                return new List<Tuple<string, string>>();
            }

            reporter.UpdateProgressBar(0, null, true);
            var uniqueFilesBag = new ConcurrentBag<Tuple<string, string>>();
            string[] supportedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".heic", ".webp" };


            try
            {
                ParallelOptions parallelOptions = new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = MaxConcurrentProcesses };
                await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        Parallel.ForEach(pathsToScanDetails, parallelOptions, (kvp, loopState) =>
                        {
                            if (token.IsCancellationRequested) { loopState.Stop(); return; }
                            string originalInputRoot = kvp.Key;
                            string actualScanRoot = kvp.Value;
                            reporter.UpdateStatusLabel($"{LocalizationHelper.GetLocalizedString("ScanningFolder", Path.GetFileName(actualScanRoot))}...");
                            try
                            {
                                if (Directory.Exists(actualScanRoot))
                                {
                                    var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MatchCasing = MatchCasing.CaseInsensitive, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
                                    foreach (var currentFilePath in Directory.EnumerateFiles(actualScanRoot, "*.*", opts))
                                    {
                                        if (token.IsCancellationRequested) { loopState.Stop(); return; }
                                        string fileExtension = Path.GetExtension(currentFilePath).ToLowerInvariant();
                                        if (supportedExtensions.Contains(fileExtension))
                                        {
                                            string relativePath = ".";
                                            try { relativePath = Path.GetRelativePath(actualScanRoot, currentFilePath); }
                                            catch (Exception ex) { reporter.LogMessage($"Warning calculating relative path for '{currentFilePath}' relative to '{actualScanRoot}': {ex.Message}"); relativePath = Path.GetFileName(currentFilePath); }
                                            string originalFilePath = Path.Combine(originalInputRoot, relativePath);
                                            uniqueFilesBag.Add(Tuple.Create(originalFilePath, currentFilePath));
                                        }
                                    }
                                }
                                else if (File.Exists(actualScanRoot))
                                {
                                    string fileExtension = Path.GetExtension(actualScanRoot).ToLowerInvariant();
                                    if (supportedExtensions.Contains(fileExtension))
                                    {
                                        uniqueFilesBag.Add(Tuple.Create(originalInputRoot, actualScanRoot));
                                    }
                                }
                            }
                            catch (OperationCanceledException) { loopState.Stop(); return; }
                            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is PathTooLongException || ex is IOException || ex is DirectoryNotFoundException)
                            { reporter.LogMessage(LocalizationHelper.GetLocalizedString("WarningScanningFolder", actualScanRoot, ex.GetType().Name, ex.Message)); }
                            catch (Exception pathEx) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorCheckingFolderPath", actualScanRoot, pathEx.Message)); }
                        });
                    }
                    catch (OperationCanceledException) { reporter.LogMessage("DEBUG: Parallel.ForEach File Collection Cancelled."); throw; }
                    catch (AggregateException aggEx) when (aggEx.InnerExceptions.Any(e => e is OperationCanceledException)) { reporter.LogMessage("DEBUG: Parallel.ForEach File Collection Cancelled Inner."); throw new OperationCanceledException("File collection cancelled.", token); }
                    catch (Exception taskRunEx) { reporter.LogMessage($"ERROR in Task.Run for file collection: {taskRunEx.Message}"); throw; }
                }, token);
            }
            catch (OperationCanceledException) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("ProcessingCancelled")); }
            catch (Exception ex) { reporter.LogMessage($"Error during parallel file collection: {ex.Message}"); }
            finally { reporter.UpdateProgressBar(0, null, false); }

            var finalUniqueFiles = uniqueFilesBag.GroupBy(t => t.Item1, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(t => t.Item1).ToList();
            reporter.LogMessage(LocalizationHelper.GetLocalizedString("CollectionComplete", finalUniqueFiles.Count));
            reporter.UpdateStatusLabel(LocalizationHelper.GetLocalizedString("CollectionComplete", finalUniqueFiles.Count));
            return finalUniqueFiles;
        }

        public string? FindBestInputPathForFile(string filePathToFindContextFor, List<string> originalInputList, IUIReporter reporter)
        {
            string fullFilePath;
            try { fullFilePath = Path.GetFullPath(filePathToFindContextFor); }
            catch (Exception ex) { reporter.LogMessage($"Error normalizing target file path '{filePathToFindContextFor}': {ex.Message}"); return Path.GetDirectoryName(filePathToFindContextFor); }

            string? bestMatchOriginalInput = null;
            int longestMatchLength = -1;

            foreach (string originalInputRaw in originalInputList)
            {
                if (string.IsNullOrWhiteSpace(originalInputRaw)) continue;
                try
                {
                    string fullInputPath = Path.GetFullPath(originalInputRaw.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (fullFilePath.Equals(fullInputPath, StringComparison.OrdinalIgnoreCase))
                    {
                        bestMatchOriginalInput = originalInputRaw;
                        break;
                    }
                    string inputPathWithSeparator = fullInputPath.EndsWith(Path.DirectorySeparatorChar.ToString())
                                                    ? fullInputPath
                                                    : fullInputPath + Path.DirectorySeparatorChar;
                    if (fullInputPath.Length > 0 && fullFilePath.StartsWith(inputPathWithSeparator, StringComparison.OrdinalIgnoreCase))
                    {
                        if (fullInputPath.Length > longestMatchLength)
                        {
                            bestMatchOriginalInput = originalInputRaw;
                            longestMatchLength = fullInputPath.Length;
                        }
                    }
                }
                catch (ArgumentException argEx) { reporter.LogMessage($"DEBUG: Path comparison error in FindBestInputPathForFile for '{originalInputRaw}' and '{filePathToFindContextFor}': {argEx.Message}"); }
                catch (Exception ex) { reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorMatchingInputPath", originalInputRaw, filePathToFindContextFor, ex.Message)); }
            }

            if (bestMatchOriginalInput != null) return bestMatchOriginalInput;

            string? parentDir = Path.GetDirectoryName(fullFilePath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                try
                {
                    string fullParentDir = Path.GetFullPath(parentDir);
                    var matchingOriginalInput = originalInputList.FirstOrDefault(orig =>
                    {
                        try { return Path.GetFullPath(orig.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Equals(fullParentDir, StringComparison.OrdinalIgnoreCase); }
                        catch { return false; }
                    });
                    if (matchingOriginalInput != null)
                    {
                        reporter.LogMessage($"Warning: Using parent directory '{fullParentDir}' as context for '{filePathToFindContextFor}' (no direct input match).");
                        return matchingOriginalInput;
                    }
                }
                catch { }
            }
            reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorNoInputContext", filePathToFindContextFor) + $" Falling back to parent directory: {parentDir}");
            return parentDir;
        }

        public string? FindFirstOutputPath(List<string> originalInputs, PathOptions pathOptions, bool backupEnabled)
        {
            string? firstProcessedOriginalContext = originalInputs.FirstOrDefault();
            if (firstProcessedOriginalContext == null) return null;

            if (CurrentView is GenerateVideoViewModel || CurrentView is GenerateBurstViewModel || CurrentView is GenerateAnimationViewModel)
            {
                if (pathOptions.UseCustomVideoOutputPath && !string.IsNullOrWhiteSpace(pathOptions.CustomVideoOutputPath))
                {
                    try { return Path.GetFullPath(pathOptions.CustomVideoOutputPath); }
                    catch (Exception ex) { _reporter.LogMessage($"Invalid custom video output path '{pathOptions.CustomVideoOutputPath}': {ex.Message}"); return null; }
                }
            }
            else
            {
                if (pathOptions.UseCustomImageOutputPath && !string.IsNullOrWhiteSpace(pathOptions.CustomImageOutputPath))
                {
                    try { return Path.GetFullPath(pathOptions.CustomImageOutputPath); }
                    catch (Exception ex) { _reporter.LogMessage($"Invalid custom image output path '{pathOptions.CustomImageOutputPath}': {ex.Message}"); return null; }
                }
            }
            try
            {
                if (originalInputs.Any())
                {
                    string firstInput = originalInputs.First();
                    if (Directory.Exists(firstInput)) return Path.GetFullPath(firstInput);
                    string? parentDir = Path.GetDirectoryName(firstInput);
                    if (parentDir != null && Directory.Exists(parentDir)) return Path.GetFullPath(parentDir);
                }
            }
            catch (Exception ex) { _reporter.LogMessage($"Error determining fallback output path: {ex.Message}"); }
            return null;
        }

        // Add these methods INSIDE the MainViewModel class

        public Dictionary<string, SourceType> GetUniqueValidSources(List<string> paths, IUIReporter reporter)
        {
            var uniqueSources = new Dictionary<string, SourceType>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    string fullPath = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (Directory.Exists(fullPath))
                    {
                        if (!uniqueSources.ContainsKey(fullPath)) uniqueSources.Add(fullPath, SourceType.Directory);
                    }
                    else if (File.Exists(fullPath))
                    {
                        if (!uniqueSources.ContainsKey(fullPath)) uniqueSources.Add(fullPath, SourceType.File);
                    }
                    else
                    {
                        reporter.LogMessage(LocalizationHelper.GetLocalizedString("IgnoringInvalidPath", path));
                    }
                }
                catch (Exception ex)
                {
                    reporter.LogMessage(LocalizationHelper.GetLocalizedString("ErrorAddingPath", path ?? "null", ex.Message));
                }
            }
            return uniqueSources;
        }

        public string? FindCommonParentDirectory(IEnumerable<string> paths)
        {
            if (paths == null || !paths.Any()) return null;

            string? commonParent = null;
            bool first = true;

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                string normalizedPath;
                try
                {
                    normalizedPath = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                }
                catch (Exception ex)
                {
                    _reporter.LogMessage($"Warning: Could not normalize path '{path}' for common parent check: {ex.Message}");
                    continue;
                }

                string? parent = Path.GetDirectoryName(normalizedPath);

                if (parent == null)
                {
                    if (first)
                    {
                        commonParent = normalizedPath;
                        first = false;
                    }
                    else if (commonParent != null && !normalizedPath.Equals(commonParent, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                    continue;
                }


                if (first)
                {
                    commonParent = parent;
                    first = false;
                }
                else if (commonParent != null && !parent.StartsWith(commonParent, StringComparison.OrdinalIgnoreCase) && !parent.Equals(commonParent, StringComparison.OrdinalIgnoreCase))
                {
                    string tempCommon = commonParent;
                    while (tempCommon != null && !parent.StartsWith(tempCommon, StringComparison.OrdinalIgnoreCase)) // Added null check for tempCommon
                    {
                        tempCommon = Path.GetDirectoryName(tempCommon);
                        if (string.IsNullOrEmpty(tempCommon))
                        {
                            return null;
                        }
                    }
                    commonParent = tempCommon;
                }
                else if (commonParent == null)
                {
                    return null;
                }
            }
            return first ? null : commonParent;
        }


    }
}