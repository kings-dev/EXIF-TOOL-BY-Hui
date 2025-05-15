// ViewModels/GenerateAnimationViewModel.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Hui_WPF.Core;
using Hui_WPF.Models;
using Hui_WPF.Utils;

namespace Hui_WPF.ViewModels
{
    public class GenerateAnimationViewModel : ViewModelBase, IProcessingTaskViewModel
    {
        private readonly IUIReporter _reporter;
        private readonly FileNamer _fileNamer = new FileNamer();
        private readonly MainViewModel _mainViewModel;
        private MediaGenerator? _mediaGenerator; // Make nullable and initialize in constructor

        private string _animationFileName = "MyAnimation";
        public string AnimationFileName { get => _animationFileName; set => SetProperty(ref _animationFileName, value?.Trim() ?? "MyAnimation"); }

        private int _frameDelayMs = 100;
        public int FrameDelayMs { get => _frameDelayMs; set => SetProperty(ref _frameDelayMs, value); }

        private string _selectedFormat = "GIF";
        public string SelectedFormat { get => _selectedFormat; set => SetProperty(ref _selectedFormat, value); }

        private bool _loopAnimation = true;
        public bool LoopAnimation { get => _loopAnimation; set => SetProperty(ref _loopAnimation, value); }

        private NamingOptions _globalNamingOptions = NamingOptionsDefaults.Default;
        private PathOptions _customPathOptions = PathOptionsDefaults.Default;
        private bool _generalEnableBackup = true;
        private bool _useTimestampSubfolderForMedia = true; // From MainViewModel global settings
        private bool _overwriteOutput = true; // From MainViewModel global settings

        public GenerateAnimationViewModel(IUIReporter reporter, MainViewModel mainViewModel)
        {
            _reporter = reporter;
            _mainViewModel = mainViewModel;
            InitializeMediaGenerator();
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
                _reporter.LogMessage(errorMsg + " GenerateAnimationViewModel may not function correctly.");
                _mediaGenerator = null;
            }
            else
            {
                _mediaGenerator = new MediaGenerator(ffmpegPath, ffprobePath);
            }
        }

        public void LoadSettings(NamingOptions globalNamingOptions, PathOptions customPathOptions, bool generalEnableBackup, int originalFileActionIndex, string outputImageFormat, int jpegQuality, string selectedExifToolTag)
        {
            _globalNamingOptions = globalNamingOptions;
            _customPathOptions = customPathOptions;
            _generalEnableBackup = generalEnableBackup;
            _useTimestampSubfolderForMedia = _mainViewModel.UseTimestampSubfolderForMedia; // Get from MainViewModel
            _overwriteOutput = _mainViewModel.OverwriteOutput; // Get from MainViewModel
        }

        public async Task ExecuteAsync(List<string> inputPaths, CancellationToken token, IUIReporter reporter)
        {
            if (_mediaGenerator == null || !File.Exists(_mediaGenerator.FFmpegPath) || !File.Exists(_mediaGenerator.FFprobePath))
            {
                string errorMsg = "";
                if (_mediaGenerator == null || !File.Exists(_mediaGenerator.FFmpegPath)) errorMsg += reporter.GetLocalizedString("FFmpegNotFound") + Environment.NewLine;
                if (_mediaGenerator == null || !File.Exists(_mediaGenerator.FFprobePath)) errorMsg += reporter.GetLocalizedString("FFprobeNotFound") + Environment.NewLine;
                reporter.ShowMessage(errorMsg, reporter.GetLocalizedString("ToolNotFoundTitle", "Tool Not Found"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                reporter.LogMessage(errorMsg);
                return;
            }

            reporter.LogMessage(reporter.GetLocalizedString("StartingAnimationGeneration"));
            reporter.UpdateStatusLabel(reporter.GetLocalizedString("StartingAnimationGeneration"));
            reporter.UpdateProgressBar(0, indeterminate: true);

            reporter.LogMessage(_generalEnableBackup ? reporter.GetLocalizedString("ExifMode_BackupEnabled") : reporter.GetLocalizedString("ExifMode_BackupDisabled"));
            Dictionary<string, string> sourceToBackupPathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_generalEnableBackup)
            {
                reporter.LogMessage(reporter.GetLocalizedString("StartingBackup"));
                reporter.UpdateProgressBar(0, indeterminate: true);
                bool backupSuccess = await _mainViewModel.PerformPreProcessingBackupAsync(inputPaths, _globalNamingOptions, _customPathOptions, _generalEnableBackup, token, reporter, sourceToBackupPathMap);
                reporter.UpdateProgressBar(0, indeterminate: false);
                if (!backupSuccess)
                {
                    reporter.LogMessage(reporter.GetLocalizedString("BackupFailedAbort"));
                    reporter.UpdateStatusLabel(reporter.GetLocalizedString("BackupFailedAbort"));
                    return;
                }
                reporter.LogMessage(reporter.GetLocalizedString("BackupComplete"));
            }

            List<Tuple<string, string>> filesToProcessDetails = await _mainViewModel.CollectAllUniqueFilesAndTheirOriginalPathsAsync(inputPaths, _generalEnableBackup, sourceToBackupPathMap, token, reporter);

            if (!filesToProcessDetails.Any())
            {
                reporter.LogMessage(reporter.GetLocalizedString("NoImagesFound"));
                reporter.UpdateStatusLabel(reporter.GetLocalizedString("NoImagesFound"));
                reporter.UpdateProgressBar(0, indeterminate: false);
                return;
            }

            int totalItems = 1;
            int processedItems = 0;
            int failedItems = 0;
            reporter.UpdateCounts(0, 0, totalItems);
            reporter.UpdateProgressBar(0, totalItems, false);

            OutputFormat outputFormatEnum = SelectedFormat.Equals("GIF", StringComparison.OrdinalIgnoreCase) ? OutputFormat.GIF : OutputFormat.MP4;
            string outputExtension = outputFormatEnum == OutputFormat.GIF ? ".gif" : ".mp4";

            string? validatedCustomVideoOutputPath = _customPathOptions.UseCustomVideoOutputPath ?
                   await _mainViewModel.GetValidatedCustomPathAsync(_customPathOptions.CustomVideoOutputPath, true, "VideoOutput", reporter) : null;
            string baseOutputDirectory = _mainViewModel.DetermineBaseOutputDirectory(validatedCustomVideoOutputPath, _useTimestampSubfolderForMedia, _globalNamingOptions.IncludeTimestamp, _mainViewModel.GetValidatedTimestampString(_globalNamingOptions.TimestampFormat, DateTime.Now));

            var renamingCounters = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string outputFilenameBase = AnimationFileName;
            int counterValue = renamingCounters.AddOrUpdate(baseOutputDirectory, _globalNamingOptions.CounterStartValue, (key, existing) => existing + 1);

            string? finalOutputFile = _fileNamer.GenerateNewFilePath(
                Path.Combine("DummyInputPath", outputFilenameBase + outputExtension),
                _globalNamingOptions, counterValue, baseOutputDirectory,
                !_overwriteOutput
            );

            if (finalOutputFile == null)
            {
                reporter.LogMessage(reporter.GetLocalizedString("ErrorUniqueAnimation", outputFilenameBase));
                failedItems++;
                reporter.UpdateCounts(processedItems, failedItems, totalItems);
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                return;
            }

            reporter.LogMessage($"Generating Animation '{outputFilenameBase}' ({SelectedFormat})...");
            reporter.UpdateStatusLabel($"Generating Animation ({SelectedFormat})...");
            Stopwatch singleItemStopwatch = Stopwatch.StartNew();
            string? fileListPath = null;

            try
            {
                fileListPath = Path.Combine(Path.GetTempPath(), $"anim_list_{Guid.NewGuid()}.txt");
                await File.WriteAllLinesAsync(fileListPath,
                    filesToProcessDetails.Select(t => $"file '{t.Item2.Replace("'", "'\\''")}'"),
                    Encoding.UTF8, token);

                double frameDurationSeconds = FrameDelayMs / 1000.0;
                var result = await _mediaGenerator.GenerateAnimationFromFilesAsync(
                   fileListPath, finalOutputFile, frameDurationSeconds, outputFormatEnum, LoopAnimation,
                   new Progress<string>(s => reporter.UpdateStatusLabel(s)), token
                );
                singleItemStopwatch.Stop();
                if (result.Success)
                {
                    processedItems++;
                    reporter.LogMessage(reporter.GetLocalizedString("SuccessAnimation", outputFilenameBase, Path.GetFileName(result.OutputFilePath!), singleItemStopwatch.Elapsed.TotalSeconds));
                }
                else
                {
                    failedItems++;
                    reporter.LogMessage(reporter.GetLocalizedString("FailedAnimation", outputFilenameBase, result.ExitCode, result.ErrorMessage ?? "Unknown Error", singleItemStopwatch.Elapsed.TotalSeconds));
                }
            }
            catch (OperationCanceledException)
            {
                reporter.LogMessage(reporter.GetLocalizedString("Debug_TaskCancelledGeneric", outputFilenameBase));
            }
            catch (Exception ex)
            {
                failedItems++;
                reporter.LogMessage(reporter.GetLocalizedString("UnexpectedErrorProcessingFile", outputFilenameBase, ex.GetType().Name, ex.Message));
                singleItemStopwatch.Stop();
                if (finalOutputFile != null && File.Exists(finalOutputFile))
                {
                    try { File.Delete(finalOutputFile); } catch { }
                }
            }
            finally
            {
                if (fileListPath != null && File.Exists(fileListPath)) { try { File.Delete(fileListPath); } catch { } }
                reporter.UpdateCounts(processedItems, failedItems, totalItems);
                reporter.UpdateProgressBar(processedItems + failedItems, totalItems, false);
                reporter.UpdateStatusLabel(reporter.GetLocalizedString("ZoompanGenerationComplete", processedItems, failedItems));
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
        }

    }
}