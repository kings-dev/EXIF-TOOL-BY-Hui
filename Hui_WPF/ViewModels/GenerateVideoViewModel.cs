// ViewModels/GenerateVideoViewModel.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Hui_WPF.Core;
using Hui_WPF.Models;
using Hui_WPF.Utils;

namespace Hui_WPF.ViewModels
{
    public class GenerateVideoViewModel : ViewModelBase, IProcessingTaskViewModel
    {
        private readonly IUIReporter _reporter;
        private readonly FileNamer _fileNamer = new FileNamer();
        private readonly MainViewModel _mainViewModel;
        private MediaGenerator? _mediaGenerator;

        private ZoompanSettings _currentSettings = new ZoompanSettings();
        public ZoompanSettings CurrentSettings
        {
            get => _currentSettings;
            set { if (SetProperty(ref _currentSettings, value)) UpdateControlsEnablement(); }
        }

        private OutputFormat _currentOutputFormat = OutputFormat.MP4;
        public OutputFormat CurrentOutputFormat { get => _currentOutputFormat; set => SetProperty(ref _currentOutputFormat, value); }

        private string _selectedResolutionTag = "source";
        public string SelectedResolutionTag { get => _selectedResolutionTag; set => SetProperty(ref _selectedResolutionTag, value); }

        public bool IsCustomEffectSelected => CurrentSettings?.EffectType == ZoompanEffectType.Custom;
        public bool IsCustomExpressionSelected => CurrentSettings?.EffectType == ZoompanEffectType.CustomExpression;
        public bool AreStandardControlsEnabled => CurrentSettings?.EffectType != ZoompanEffectType.CustomExpression;

        public ObservableCollection<CustomZoompanPreset> SavedCustomPresets { get; set; } = new ObservableCollection<CustomZoompanPreset>();
        private CustomZoompanPreset? _selectedCustomPreset;
        public CustomZoompanPreset? SelectedCustomPreset
        {
            get => _selectedCustomPreset;
            set
            {
                if (_isProgrammaticChange) { SetProperty(ref _selectedCustomPreset, value); return; }
                _isProgrammaticChange = true;
                if (SetProperty(ref _selectedCustomPreset, value))
                {
                    if (value != null) { ApplyCustomPreset(value); }
                    else if (CurrentSettings.EffectType == ZoompanEffectType.CustomExpression)
                    { CurrentSettings.EffectType = ZoompanEffectType.ZoomInCenterSlow; }
                    ((RelayCommand)DeletePresetCommand).RaiseCanExecuteChanged();
                }
                _isProgrammaticChange = false;
            }
        }

        private string _newPresetName = "";
        //public string NewPresetName { get => _newPresetName; set { if (SetProperty(ref _newPresetName, value?.Trim() ?? "")) ; ((RelayCommand)SavePresetCommand).RaiseCanExecuteChanged(); } }
        public string NewPresetName { get => _newPresetName; set { if (SetProperty(ref _newPresetName, value?.Trim() ?? "")) ((RelayCommand)SavePresetCommand).RaiseCanExecuteChanged(); } }

        public string NewPresetPlaceholderText => _reporter.GetLocalizedString("NewPresetDefaultName", "新预设名称");


        public ICommand SavePresetCommand { get; }
        public ICommand DeletePresetCommand { get; }

        private readonly string? CustomPresetsFilePath; // Made nullable
        private bool _isProgrammaticChange = false;

        private NamingOptions _globalNamingOptions = NamingOptionsDefaults.Default;
        private PathOptions _customPathOptions = PathOptionsDefaults.Default;
        private bool _generalEnableBackup = true;
        private bool _useTimestampSubfolderForMedia = true;
        private bool _overwriteOutput = true;


        public GenerateVideoViewModel(IUIReporter reporter, MainViewModel mainViewModel)
        {
            _reporter = reporter;
            _mainViewModel = mainViewModel;

            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appDataPath, "ExifDog_VideoPresets");
            try { Directory.CreateDirectory(appFolder); CustomPresetsFilePath = Path.Combine(appFolder, "VideoGenPresets.json"); }
            catch (Exception ex)
            {
                _reporter.LogMessage($"Error creating preset folder '{appFolder}': {ex.Message}");
                CustomPresetsFilePath = null; // Cannot save/load presets
            }


            InitializeMediaGenerator();

            SavePresetCommand = new RelayCommand(ExecuteSavePreset, CanSavePreset);
            DeletePresetCommand = new RelayCommand(ExecuteDeletePreset, CanDeletePreset);

            if (CurrentSettings != null) // Ensure CurrentSettings is not null before subscribing
            {
                CurrentSettings.PropertyChanged += Settings_PropertyChanged;
            }


            if (CustomPresetsFilePath != null) LoadCustomPresets();
            NewPresetName = NewPresetPlaceholderText;
            LoadComboBoxDefaults();
            UpdateControlsEnablement();
        }
        private void InitializeMediaGenerator()
        {
            string? ffmpegPath = _reporter.FindToolPathExternal("ffmpeg.exe");
            string? ffprobePath = _reporter.FindToolPathExternal("ffprobe.exe");

            if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath) || string.IsNullOrEmpty(ffprobePath) || !File.Exists(ffprobePath))
            {
                string errorMsg = "";
                if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath)) errorMsg += _reporter.GetLocalizedString("FFmpegNotFound") + Environment.NewLine;
                if (string.IsNullOrEmpty(ffprobePath) || !File.Exists(ffprobePath)) errorMsg += _reporter.GetLocalizedString("FFprobeNotFound") + Environment.NewLine;
                _reporter.LogMessage(errorMsg + " GenerateVideoViewModel may not function correctly.");
                _mediaGenerator = null;
            }
            else
            {
                _mediaGenerator = new MediaGenerator(ffmpegPath, ffprobePath);
            }
        }


        private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isProgrammaticChange || CurrentSettings == null) return;
            if (e.PropertyName == nameof(ZoompanSettings.EffectType))
            {
                _isProgrammaticChange = true;
                UpdateControlsEnablement();
                if (CurrentSettings.EffectType != ZoompanEffectType.Custom &&
                    CurrentSettings.EffectType != ZoompanEffectType.CustomExpression &&
                    CurrentSettings.EffectType != ZoompanEffectType.RandomPreset)
                {
                    LoadPresetDefaults(CurrentSettings.EffectType);
                }
                if (CurrentSettings.EffectType == ZoompanEffectType.CustomExpression)
                {
                    var matchingPreset = SavedCustomPresets.FirstOrDefault(p => p.Expression == CurrentSettings.CustomFilterExpression);
                    if (SelectedCustomPreset != matchingPreset) SelectedCustomPreset = matchingPreset;
                }
                else
                {
                    if (SelectedCustomPreset != null) SelectedCustomPreset = null;
                }
                _isProgrammaticChange = false;
                ((RelayCommand)SavePresetCommand).RaiseCanExecuteChanged();
            }
            else if (e.PropertyName == nameof(ZoompanSettings.CustomFilterExpression))
            {
                if (CurrentSettings.EffectType == ZoompanEffectType.CustomExpression)
                {
                    var matchingPreset = SavedCustomPresets.FirstOrDefault(p => p.Expression == CurrentSettings.CustomFilterExpression);
                    if (SelectedCustomPreset != matchingPreset) SelectedCustomPreset = matchingPreset;
                }
                 ((RelayCommand)SavePresetCommand).RaiseCanExecuteChanged();
            }
        }

        private void UpdateControlsEnablement()
        {
            OnPropertyChanged(nameof(IsCustomEffectSelected));
            OnPropertyChanged(nameof(IsCustomExpressionSelected));
            OnPropertyChanged(nameof(AreStandardControlsEnabled));
            ((RelayCommand)SavePresetCommand).RaiseCanExecuteChanged();
            ((RelayCommand)DeletePresetCommand).RaiseCanExecuteChanged();
        }

        private void LoadPresetDefaults(ZoompanEffectType preset)
        {
            if (CurrentSettings == null) return;
            if (preset == ZoompanEffectType.Custom || preset == ZoompanEffectType.CustomExpression || preset == ZoompanEffectType.RandomPreset) return;
            double newZoom = CurrentSettings.TargetZoom; PanDirection newPan = CurrentSettings.PanDirection;
            switch (preset)
            {
                case ZoompanEffectType.ZoomInCenterSlow: newPan = PanDirection.None; newZoom = 1.5; break;
                case ZoompanEffectType.ZoomInCenterFast: newPan = PanDirection.None; newZoom = 1.8; break;
                case ZoompanEffectType.ZoomOutCenter: newPan = PanDirection.None; newZoom = 1.0; break;
                case ZoompanEffectType.PanRight: newPan = PanDirection.Right; newZoom = 1.0; break;
                case ZoompanEffectType.PanLeft: newPan = PanDirection.Left; newZoom = 1.0; break;
                case ZoompanEffectType.PanUp: newPan = PanDirection.Up; newZoom = 1.0; break;
                case ZoompanEffectType.PanDown: newPan = PanDirection.Down; newZoom = 1.0; break;
                case ZoompanEffectType.ZoomInPanTopRight: newPan = PanDirection.None; newZoom = 1.6; break;
                case ZoompanEffectType.ZoomInPanBottomLeft: newPan = PanDirection.None; newZoom = 1.6; break;
                case ZoompanEffectType.IphoneStyle: newPan = PanDirection.None; newZoom = 1.25; break;
            }
            _isProgrammaticChange = true;
            if (CurrentSettings.PanDirection != newPan) CurrentSettings.PanDirection = newPan;
            if (Math.Abs(CurrentSettings.TargetZoom - newZoom) > 0.001) CurrentSettings.TargetZoom = newZoom;
            _isProgrammaticChange = false;
        }

        public void ApplyCustomPreset(CustomZoompanPreset preset)
        {
            if (preset == null || _isProgrammaticChange || CurrentSettings == null) return;
            _isProgrammaticChange = true;
            CurrentSettings.CustomFilterExpression = preset.Expression;
            CurrentSettings.EffectType = ZoompanEffectType.CustomExpression;
            _isProgrammaticChange = false;
            _reporter.LogMessage(_reporter.GetLocalizedString("ZoompanSettingsUpdatedMsg"));
            ((RelayCommand)SavePresetCommand).RaiseCanExecuteChanged();
        }

        private bool CanSavePreset()
        {
            return IsCustomExpressionSelected &&
                   !string.IsNullOrWhiteSpace(CurrentSettings?.CustomFilterExpression) &&
                   !string.IsNullOrWhiteSpace(NewPresetName) &&
                   NewPresetName != NewPresetPlaceholderText;
        }

        private async void ExecuteSavePreset()
        {
            if (!CanSavePreset() || CurrentSettings == null) return;
            string presetName = NewPresetName.Trim();
            string expression = CurrentSettings.CustomFilterExpression.Trim();
            var existing = SavedCustomPresets.FirstOrDefault(p => p.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));
            bool proceed = true;
            if (existing != null)
            {
                string confirmMsg = _reporter.GetLocalizedString("PresetOverwriteConfirm", $"名为 '{presetName}' 的预设已存在。\n要覆盖它吗？");
                string confirmTitle = _reporter.GetLocalizedString("PresetOverwriteTitle", "覆盖确认");
                var result = await _reporter.ShowMessageAsync(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning, CancellationToken.None);
                proceed = result == MessageBoxResult.Yes;
                if (proceed) SavedCustomPresets.Remove(existing);
            }
            if (proceed)
            {
                var newPreset = new CustomZoompanPreset(presetName, expression);
                SavedCustomPresets.Add(newPreset);
                var sorted = new ObservableCollection<CustomZoompanPreset>(SavedCustomPresets.OrderBy(p => p.Name));
                SavedCustomPresets.Clear(); foreach (var p in sorted) SavedCustomPresets.Add(p);
                SaveCustomPresetsToFile();
                _isProgrammaticChange = true; SelectedCustomPreset = newPreset; _isProgrammaticChange = false;
                NewPresetName = NewPresetPlaceholderText;
                _reporter.LogMessage(_reporter.GetLocalizedString("PresetSavedMsg", presetName));
            }
            ((RelayCommand)DeletePresetCommand).RaiseCanExecuteChanged();
        }

        private bool CanDeletePreset() => SelectedCustomPreset != null;

        private async void ExecuteDeletePreset()
        {
            if (!CanDeletePreset()) return;
            CustomZoompanPreset presetToDelete = SelectedCustomPreset!;
            string confirmMsg = _reporter.GetLocalizedString("DeletePresetConfirm", $"确定要删除预设 '{presetToDelete.Name}' 吗？");
            string confirmTitle = _reporter.GetLocalizedString("DeletePresetTitle", "删除确认");
            var result = await _reporter.ShowMessageAsync(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning, CancellationToken.None);
            if (result == MessageBoxResult.Yes)
            {
                string deletedExpression = presetToDelete.Expression;
                if (SavedCustomPresets.Remove(presetToDelete))
                {
                    SaveCustomPresetsToFile();
                    _reporter.LogMessage(_reporter.GetLocalizedString("PresetDeletedMsg", presetToDelete.Name));
                    if (CurrentSettings.EffectType == ZoompanEffectType.CustomExpression && CurrentSettings.CustomFilterExpression == deletedExpression)
                    {
                        _isProgrammaticChange = true; CurrentSettings.EffectType = ZoompanEffectType.ZoomInCenterSlow; _isProgrammaticChange = false;
                        _reporter.LogMessage(_reporter.GetLocalizedString("DeletedPresetWasActiveMsg"));
                    }
                }
                else { _reporter.ShowMessage("无法从内部集合中移除预设。", "删除错误", MessageBoxButton.OK, MessageBoxImage.Error); _reporter.LogMessage("Error: Failed to remove preset from collection."); }
            }
            ((RelayCommand)DeletePresetCommand).RaiseCanExecuteChanged();
        }

        private void LoadCustomPresets()
        {
            if (string.IsNullOrEmpty(CustomPresetsFilePath) || !File.Exists(CustomPresetsFilePath)) { _reporter.LogMessage("No custom preset file found or path is invalid."); return; }
            SavedCustomPresets.Clear();
            try
            {
                string json = File.ReadAllText(CustomPresetsFilePath);
                var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All), AllowTrailingCommas = true };
                var loaded = JsonSerializer.Deserialize<List<CustomZoompanPreset>>(json, options);
                if (loaded != null) { foreach (var p in loaded.OrderBy(pr => pr.Name)) SavedCustomPresets.Add(p); }
                _reporter.LogMessage($"Loaded {SavedCustomPresets.Count} custom presets from {Path.GetFileName(CustomPresetsFilePath)}.");
            }
            catch (Exception ex) { _reporter.LogMessage($"Error loading custom presets from {Path.GetFileName(CustomPresetsFilePath)}: {ex.Message}"); _reporter.ShowMessage($"Error loading custom presets: {ex.Message}", _reporter.GetLocalizedString("LoadErrorTitle", "Load Error"), MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SaveCustomPresetsToFile()
        {
            if (string.IsNullOrEmpty(CustomPresetsFilePath)) { _reporter.LogMessage("Custom preset file path is not set. Cannot save."); return; }
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
                string json = JsonSerializer.Serialize(SavedCustomPresets.ToList(), options);
                File.WriteAllText(CustomPresetsFilePath, json);
                _reporter.LogMessage($"Saved {SavedCustomPresets.Count} custom presets to {Path.GetFileName(CustomPresetsFilePath)}.");
            }
            catch (Exception ex) { _reporter.LogMessage($"Error saving custom presets to {Path.GetFileName(CustomPresetsFilePath)}: {ex.Message}"); _reporter.ShowMessage($"无法保存预设: {ex.Message}", _reporter.GetLocalizedString("SaveErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void UpdateValueTextBlocks() { }

        private void LoadComboBoxDefaults()
        {
            if (CurrentSettings == null) CurrentSettings = new ZoompanSettings();
            if (CurrentSettings.Fps <= 0) CurrentSettings.Fps = 30;
            if (CurrentSettings.BurstFramerate <= 0) CurrentSettings.BurstFramerate = 15;
            if (string.IsNullOrWhiteSpace(SelectedResolutionTag)) SelectedResolutionTag = "source";
        }

        public void LoadSettings(NamingOptions globalNamingOptions, PathOptions customPathOptions, bool generalEnableBackup, int originalFileActionIndex, string outputImageFormat, int jpegQuality, string selectedExifToolTag)
        {
            _globalNamingOptions = globalNamingOptions;
            _customPathOptions = customPathOptions;
            _generalEnableBackup = generalEnableBackup;
            _useTimestampSubfolderForMedia = _mainViewModel.UseTimestampSubfolderForMedia;
            _overwriteOutput = _mainViewModel.OverwriteOutput;
        }

        public async Task ExecuteAsync(List<string> inputPaths, CancellationToken token, IUIReporter reporter)
        {
            if (_mediaGenerator == null || !File.Exists(_mediaGenerator.FFmpegPath) || !File.Exists(_mediaGenerator.FFprobePath))
            {
                string errorMsg = "";
                if (_mediaGenerator == null || !File.Exists(_mediaGenerator.FFmpegPath)) errorMsg += reporter.GetLocalizedString("FFmpegNotFound") + Environment.NewLine;
                if (_mediaGenerator == null || !File.Exists(_mediaGenerator.FFprobePath)) errorMsg += reporter.GetLocalizedString("FFprobeNotFound") + Environment.NewLine;
                reporter.ShowMessage(errorMsg, reporter.GetLocalizedString("ToolNotFoundTitle", "Tool Not Found"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                reporter.LogMessage(errorMsg); return;
            }

            reporter.LogMessage(reporter.GetLocalizedString("StartingZoompanGeneration", inputPaths.Count));
            reporter.UpdateStatusLabel(reporter.GetLocalizedString("StartingZoompanGeneration", inputPaths.Count));

            reporter.LogMessage(_generalEnableBackup ? reporter.GetLocalizedString("ExifMode_BackupEnabled") : reporter.GetLocalizedString("ExifMode_BackupDisabled"));
            Dictionary<string, string> sourceToBackupPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_generalEnableBackup)
            {
                reporter.LogMessage(reporter.GetLocalizedString("StartingBackup"));
                reporter.UpdateProgressBar(0, indeterminate: true);
                bool backupSuccess = await _mainViewModel.PerformPreProcessingBackupAsync(inputPaths, _globalNamingOptions, _customPathOptions, _generalEnableBackup, token, reporter, sourceToBackupPathMap);
                reporter.UpdateProgressBar(0, indeterminate: false);
                if (!backupSuccess) { reporter.LogMessage(reporter.GetLocalizedString("BackupFailedAbort")); reporter.UpdateStatusLabel(reporter.GetLocalizedString("BackupFailedAbort")); return; }
                reporter.LogMessage(reporter.GetLocalizedString("BackupComplete"));
            }

            List<Tuple<string, string>> filesToProcessDetails = await _mainViewModel.CollectAllUniqueFilesAndTheirOriginalPathsAsync(inputPaths, _generalEnableBackup, sourceToBackupPathMap, token, reporter);
            if (!filesToProcessDetails.Any())
            {
                reporter.LogMessage(reporter.GetLocalizedString("NoImagesFound"));
                reporter.UpdateStatusLabel(reporter.GetLocalizedString("NoImagesFound"));
                reporter.UpdateProgressBar(0, indeterminate: false); return;
            }

            int totalImages = filesToProcessDetails.Count;
            int processedCountInternal = 0;
            int failedCountInternal = 0;
            reporter.UpdateCounts(processedCountInternal, failedCountInternal, totalImages);
            reporter.UpdateProgressBar(0, totalImages, false);
            reporter.LogMessage(reporter.GetLocalizedString("StartingProcessing", totalImages));

            string? validatedCustomVideoOutputPath = _customPathOptions.UseCustomVideoOutputPath ?
                   await _mainViewModel.GetValidatedCustomPathAsync(_customPathOptions.CustomVideoOutputPath, true, "VideoOutput", reporter) : null;
            string baseOutputDirectory = _mainViewModel.DetermineBaseOutputDirectory(validatedCustomVideoOutputPath, _useTimestampSubfolderForMedia, _globalNamingOptions.IncludeTimestamp, _mainViewModel.GetValidatedTimestampString(_globalNamingOptions.TimestampFormat, DateTime.Now));

            var processingTasks = new List<Task>();
            var renamingCounters = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool customOutputRequiresSubdirs = _customPathOptions.UseCustomVideoOutputPath && !string.IsNullOrWhiteSpace(_customPathOptions.CustomVideoOutputPath) &&
                                               (inputPaths.Count(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p)) > 1 || (inputPaths.Count > 1 && inputPaths.Any(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p))));

            foreach (var fileDetail in filesToProcessDetails)
            {
                if (token.IsCancellationRequested) break;
                string originalFilePath = fileDetail.Item1;
                string currentProcessingFile = fileDetail.Item2;

                processingTasks.Add(Task.Run(async () =>
                {
                    await _mainViewModel.processSemaphore.WaitAsync(token);
                    Interlocked.Increment(ref _mainViewModel._activeTasksCount);
                    reporter.UpdateActiveTasks(_mainViewModel._activeTasksCount);

                    string? finalOutputFilePath = null; Stopwatch sw = Stopwatch.StartNew(); string fileDisplayName = Path.GetFileName(originalFilePath); bool taskSuccess = false;
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        int currentItemIndexForStatus = Interlocked.Increment(ref processedCountInternal);
                        reporter.UpdateStatusLabel($"{reporter.GetLocalizedString("ZoompanStatusProcessing", fileDisplayName, currentItemIndexForStatus + failedCountInternal, totalImages)}");

                        string? originalInputItemForFile = _mainViewModel.FindBestInputPathForFile(originalFilePath, inputPaths, reporter);
                        if (string.IsNullOrEmpty(originalInputItemForFile)) throw new InvalidOperationException(reporter.GetLocalizedString("ErrorNoInputContext", originalFilePath));
                        string relativePathDir = "";
                        try
                        {
                            relativePathDir = Path.GetRelativePath(originalInputItemForFile, originalFilePath);
                            relativePathDir = Path.GetDirectoryName(relativePathDir)?.TrimStart('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "";
                        }
                        catch (Exception ex) { reporter.LogMessage(reporter.GetLocalizedString("WarnRelativePath", originalFilePath, originalInputItemForFile, ex.Message)); relativePathDir = ""; }

                        string finalOutputDirectory;
                        if (_customPathOptions.UseCustomVideoOutputPath && !string.IsNullOrWhiteSpace(_customPathOptions.CustomVideoOutputPath))
                        {
                            finalOutputDirectory = customOutputRequiresSubdirs ?
                                Path.Combine(_customPathOptions.CustomVideoOutputPath, Path.GetFileName(originalInputItemForFile), relativePathDir) :
                                Path.Combine(_customPathOptions.CustomVideoOutputPath, relativePathDir);
                        }
                        else
                        {
                            string outputSubfolder = _globalNamingOptions.OutputSubfolder ?? "Output";
                            string? originalParentDir = Path.GetDirectoryName(originalFilePath);
                            if (string.IsNullOrEmpty(originalParentDir)) throw new InvalidOperationException(reporter.GetLocalizedString("ErrorDeterminingDirectory", originalFilePath));
                            finalOutputDirectory = Path.Combine(originalParentDir, outputSubfolder, relativePathDir);
                        }
                        if (!Directory.Exists(finalOutputDirectory))
                        {
                            try { Directory.CreateDirectory(finalOutputDirectory); reporter.LogMessage(reporter.GetLocalizedString("CreatedOutputDir", finalOutputDirectory)); }
                            catch (Exception crEx) { throw new IOException(reporter.GetLocalizedString("ErrorCreatingOutputFolder", finalOutputDirectory, crEx.Message), crEx); }
                        }
                        int counterValue = renamingCounters.AddOrUpdate(finalOutputDirectory, _globalNamingOptions.CounterStartValue, (key, existing) => existing + 1);
                        string? originalFileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFilePath);
                        string newBaseFileName = _mainViewModel.ConstructNameFromGlobalSettings(
                           counterValue, DateTime.Now, Path.GetFileName(originalInputItemForFile), originalFileNameWithoutExtension
                        );
                        string outputExtension = CurrentOutputFormat switch { OutputFormat.MP4 => ".mp4", OutputFormat.GIF => ".gif", OutputFormat.MOV => ".mov", _ => ".mp4" };
                        string? generatedPath = _fileNamer.GenerateNewFilePath(
                            originalFilePath, _globalNamingOptions, counterValue, finalOutputDirectory,
                            !_overwriteOutput
                        );
                        if (generatedPath == null) throw new InvalidOperationException(reporter.GetLocalizedString("ErrorUniqueVideo", originalFileNameWithoutExtension ?? fileDisplayName));
                        finalOutputFilePath = Path.ChangeExtension(generatedPath, outputExtension);

                        var result = await _mediaGenerator!.GenerateVideoFromImageAsync(
                           currentProcessingFile, finalOutputFilePath, CurrentSettings, SelectedResolutionTag, CurrentOutputFormat,
                           new Progress<string>(msg => reporter.UpdateStatusLabel(msg)), token
                        );
                        sw.Stop();
                        if (result.Success)
                        {
                            taskSuccess = true;
                            reporter.LogMessage(reporter.GetLocalizedString("SuccessZoompan", fileDisplayName, Path.GetFileName(result.OutputFilePath!), result.DurationSeconds));
                        }
                        else
                        {
                            reporter.LogMessage(reporter.GetLocalizedString("FailedZoompan", fileDisplayName, result.ExitCode, result.ErrorMessage ?? "Unknown Error", sw.Elapsed.TotalSeconds));
                            if (finalOutputFilePath != null && File.Exists(finalOutputFilePath)) { try { File.Delete(finalOutputFilePath); } catch { } }
                        }
                    }
                    catch (OperationCanceledException) { Interlocked.Decrement(ref processedCountInternal); reporter.LogMessage(reporter.GetLocalizedString("Debug_TaskCancelledGeneric", fileDisplayName)); }
                    catch (Exception ex)
                    {
                        Interlocked.Decrement(ref processedCountInternal);
                        reporter.LogMessage(reporter.GetLocalizedString("UnexpectedErrorProcessingFile", originalFilePath, ex.GetType().Name, ex.Message));
                        sw.Stop(); if (finalOutputFilePath != null && File.Exists(finalOutputFilePath)) { try { File.Delete(finalOutputFilePath); } catch { } }
                    }
                    finally
                    {
                        if (!taskSuccess && !(token.IsCancellationRequested)) { Interlocked.Increment(ref failedCountInternal); }
                        _mainViewModel.processSemaphore.Release();
                        Interlocked.Decrement(ref _mainViewModel._activeTasksCount);
                        reporter.UpdateActiveTasks(_mainViewModel._activeTasksCount);
                        reporter.UpdateCounts(processedCountInternal, failedCountInternal, totalImages);
                    }
                }, token));
            }
            try
            {
                await Task.WhenAll(processingTasks);
                reporter.LogMessage(reporter.GetLocalizedString("Debug_ParallelProcessingFinished", processedCountInternal, failedCountInternal));
            }
            catch (OperationCanceledException) { reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllCaughtCancellation")); }
            catch (Exception ex)
            {
                reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllCaughtError", ex.GetType().Name, ex.Message));
                if (ex is AggregateException aggEx) { foreach (var innerEx in aggEx.Flatten().InnerExceptions) reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllInnerError", innerEx.GetType().Name, innerEx.Message)); }
            }
        }
    }
}