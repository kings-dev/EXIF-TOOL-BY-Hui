// ViewModels/GenerateBurstViewModel.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Hui_WPF.Core;
using Hui_WPF.Models;
using Hui_WPF.Utils;

namespace Hui_WPF.ViewModels
{
    public class GenerateBurstViewModel : ViewModelBase, IProcessingTaskViewModel
    {
        private readonly IUIReporter _reporter;
        private readonly FileNamer _fileNamer = new FileNamer();
        private readonly MainViewModel _mainViewModel;
        private readonly MediaGenerator _mediaGenerator;

        private string _outputFileNameBase = "BurstOutput";
        public string OutputFileNameBase { get => _outputFileNameBase; set => SetProperty(ref _outputFileNameBase, value?.Trim() ?? "BurstOutput"); }

        private int _framerate = 15;
        public int Framerate { get => _framerate; set => SetProperty(ref _framerate, value); }

        private OutputFormat _burstOutputFormat = OutputFormat.MP4;
        public OutputFormat BurstOutputFormat { get => _burstOutputFormat; set => SetProperty(ref _burstOutputFormat, value); }

        private NamingOptions _globalNamingOptions = NamingOptionsDefaults.Default;
        private PathOptions _customPathOptions = PathOptionsDefaults.Default;
        private bool _generalEnableBackup = true;

        public GenerateBurstViewModel(IUIReporter reporter, MainViewModel mainViewModel)
        {
            _reporter = reporter;
            _mainViewModel = mainViewModel;

            string? ffmpegPath = _reporter.FindToolPathExternal("ffmpeg.exe");
            string? ffprobePath = _reporter.FindToolPathExternal("ffprobe.exe");
            if (string.IsNullOrEmpty(ffmpegPath) || string.IsNullOrEmpty(ffprobePath))
            {
                string errorMsg = "";
                if (string.IsNullOrEmpty(ffmpegPath)) errorMsg += reporter.GetLocalizedString("FFmpegNotFound") + Environment.NewLine;
                if (string.IsNullOrEmpty(ffprobePath)) errorMsg += reporter.GetLocalizedString("FFprobeNotFound") + Environment.NewLine;
                _reporter.ShowMessage(errorMsg, reporter.GetLocalizedString("ToolNotFoundTitle", "Tool Not Found"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                _reporter.LogMessage(errorMsg);
                _mediaGenerator = new MediaGenerator(ffmpegPath ?? "", ffprobePath ?? "");
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
        }

        public async Task ExecuteAsync(List<string> inputPaths, CancellationToken token, IUIReporter reporter)
        {
            if (inputPaths.Count != 1 || !Directory.Exists(inputPaths.First()))
            {
                reporter.ShowMessage(reporter.GetLocalizedString("BurstModeWarning"), reporter.GetLocalizedString("Tip"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                reporter.LogMessage(reporter.GetLocalizedString("BurstModeWarning"));
                return;
            }
            if (!File.Exists(_mediaGenerator.FFmpegPath) || !File.Exists(_mediaGenerator.FFprobePath))
            {
                string errorMsg = "";
                if (!File.Exists(_mediaGenerator.FFmpegPath)) errorMsg += reporter.GetLocalizedString("FFmpegNotFound") + Environment.NewLine;
                if (!File.Exists(_mediaGenerator.FFprobePath)) errorMsg += reporter.GetLocalizedString("FFprobeNotFound") + Environment.NewLine;
                reporter.ShowMessage(errorMsg, reporter.GetLocalizedString("ToolNotFoundTitle", "Tool Not Found"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                reporter.LogMessage(errorMsg);
                return;
            }

            string originalInputFolder = inputPaths.First();
            string currentProcessingFolder = originalInputFolder;

            reporter.LogMessage(reporter.GetLocalizedString("StartingBurstGeneration", Path.GetFileName(originalInputFolder)));
            reporter.UpdateStatusLabel(reporter.GetLocalizedString("StartingBurstGeneration", Path.GetFileName(originalInputFolder)));
            int totalItems = 1;
            int processedItems = 0;
            int failedItems = 0;
            reporter.UpdateCounts(0, 0, totalItems);
            reporter.UpdateProgressBar(0, totalItems, false);

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
                if (sourceToBackupPathMap.TryGetValue(originalInputFolder, out string? backupPath))
                {
                    currentProcessingFolder = backupPath;
                }
                else
                {
                    reporter.LogMessage(_reporter.GetLocalizedString("BackupMapEntryNotFound", originalInputFolder));
                    failedItems++; reporter.UpdateCounts(processedItems, failedItems, totalItems);
                    _mainViewModel.StartProcessingCommand.RaiseCanExecuteChanged(); return;
                }
            }

            if (!Directory.Exists(currentProcessingFolder))
            {
                reporter.LogMessage($"Burst source folder not found: {currentProcessingFolder}");
                failedItems++; reporter.UpdateCounts(processedItems, failedItems, totalItems);
                _mainViewModel.StartProcessingCommand.RaiseCanExecuteChanged(); return;
            }
            var imageFiles = Directory.EnumerateFiles(currentProcessingFolder).Where(f => FileDialogHelper.IsSupportedImageExtension(f)).ToList();
            if (!imageFiles.Any())
            {
                reporter.LogMessage(reporter.GetLocalizedString("BurstModeNoImages"));
                reporter.UpdateProgressBar(0, indeterminate: false); return;
            }

            string? validatedCustomVideoOutputPath = _customPathOptions.UseCustomVideoOutputPath ?
                   await _mainViewModel.GetValidatedCustomPathAsync(_customPathOptions.CustomVideoOutputPath, true, "VideoOutput", reporter) : null;
            string baseOutputDirectory = _mainViewModel.DetermineBaseOutputDirectory(validatedCustomVideoOutputPath, _mainViewModel.UseTimestampSubfolderForMedia, _globalNamingOptions.IncludeTimestamp, _mainViewModel.GetValidatedTimestampString(_globalNamingOptions.TimestampFormat, DateTime.Now));

            var renamingCounters = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string outputExtension = BurstOutputFormat switch { OutputFormat.MP4 => ".mp4", OutputFormat.GIF => ".gif", OutputFormat.MOV => ".mov", _ => ".mp4" };
            int counterValue = renamingCounters.AddOrUpdate(baseOutputDirectory, _globalNamingOptions.CounterStartValue, (key, existing) => existing + 1);
            string? finalOutputFile = _fileNamer.GenerateNewFilePath(
                Path.Combine("DummyInputPath", OutputFileNameBase + outputExtension),
                _globalNamingOptions, counterValue, baseOutputDirectory,
                !_mainViewModel.OverwriteOutput
            );

            if (finalOutputFile == null)
            {
                reporter.LogMessage(reporter.GetLocalizedString("ErrorUniqueBurst", OutputFileNameBase));
                failedItems++; reporter.UpdateCounts(processedItems, failedItems, totalItems);
                _mainViewModel.StartProcessingCommand.RaiseCanExecuteChanged(); return;
            }

            reporter.LogMessage($"Start Generating Burst Mode file ({BurstOutputFormat}) from '{Path.GetFileName(originalInputFolder)}'...");
            reporter.UpdateStatusLabel($"Generating Burst ({BurstOutputFormat})...");
            Stopwatch singleItemStopwatch = Stopwatch.StartNew();

            try
            {
                var result = await _mediaGenerator.GenerateBurstMediaAsync(
                   currentProcessingFolder, finalOutputFile, Framerate, BurstOutputFormat,
                   new Progress<string>(s => reporter.UpdateStatusLabel(s)), token
                );
                singleItemStopwatch.Stop();
                if (result.Success)
                {
                    processedItems++;
                    reporter.LogMessage(reporter.GetLocalizedString("SuccessBurst", Path.GetFileName(originalInputFolder), Path.GetFileName(result.OutputFilePath!), singleItemStopwatch.Elapsed.TotalSeconds));
                }
                else
                {
                    failedItems++;
                    reporter.LogMessage(reporter.GetLocalizedString("FailedBurst", Path.GetFileName(originalInputFolder), result.ExitCode, result.ErrorMessage ?? "Unknown Error", singleItemStopwatch.Elapsed.TotalSeconds));
                }
            }
            catch (OperationCanceledException)
            {
                reporter.LogMessage(reporter.GetLocalizedString("Debug_TaskCancelledGeneric", Path.GetFileName(originalInputFolder)));
            }
            catch (Exception ex)
            {
                failedItems++;
                reporter.LogMessage(reporter.GetLocalizedString("UnexpectedErrorProcessingFile", Path.GetFileName(originalInputFolder), ex.GetType().Name, ex.Message));
                singleItemStopwatch.Stop();
                if (finalOutputFile != null && File.Exists(finalOutputFile)) { try { File.Delete(finalOutputFile); } catch { } }
            }
            finally
            {
                reporter.UpdateCounts(processedItems, failedItems, totalItems);
                reporter.UpdateProgressBar(processedItems + failedItems, totalItems, false);
                reporter.UpdateStatusLabel(reporter.GetLocalizedString("ZoompanGenerationComplete", processedItems, failedItems));
                _mainViewModel.StartProcessingCommand.RaiseCanExecuteChanged();
            }
        }
    }
}