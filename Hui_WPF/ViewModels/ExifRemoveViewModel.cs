// ViewModels/ExifRemoveViewModel.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class ExifRemoveViewModel : ViewModelBase, IProcessingTaskViewModel
    {
        private readonly IUIReporter _reporter;
        private ExifProcessor? _exifProcessor;
        private readonly FileNamer _fileNamer = new FileNamer();
        private readonly MainViewModel _mainViewModel;

        private bool _removeAllMetadata = true;
        public bool RemoveAllMetadata
        {
            get => _removeAllMetadata;
            set { if (SetProperty(ref _removeAllMetadata, value)) OnPropertyChanged(nameof(CanKeepSpecific)); }
        }
        private bool _keepDateTaken = false;
        public bool KeepDateTaken { get => _keepDateTaken; set => SetProperty(ref _keepDateTaken, value); }
        private bool _keepGps = false;
        public bool KeepGps { get => _keepGps; set => SetProperty(ref _keepGps, value); }
        private bool _keepOrientation = false;
        public bool KeepOrientation { get => _keepOrientation; set => SetProperty(ref _keepOrientation, value); }
        private bool _keepCameraInfo = false;
        public bool KeepCameraInfo { get => _keepCameraInfo; set => SetProperty(ref _keepCameraInfo, value); }
        private bool _keepColorSpace = false;
        public bool KeepColorSpace { get => _keepColorSpace; set => SetProperty(ref _keepColorSpace, value); }
        private bool _removeThumbnail = true;
        public bool RemoveThumbnail { get => _removeThumbnail; set => SetProperty(ref _removeThumbnail, value); }
        public bool CanKeepSpecific => !RemoveAllMetadata;

        private NamingOptions _globalNamingOptions = NamingOptionsDefaults.Default;
        private PathOptions _customPathOptions = PathOptionsDefaults.Default;
        private bool _generalEnableBackup = true;
        private int _originalFileActionIndex = 0;
        private string _selectedExifToolTag = "ExifTool";


        public ExifRemoveViewModel(IUIReporter reporter, MainViewModel mainViewModel)
        {
            _reporter = reporter;
            _mainViewModel = mainViewModel;
            InitializeExifProcessor();
        }

        private void InitializeExifProcessor()
        {
            string? toolPath = _reporter.FindToolPathExternal(_selectedExifToolTag) ?? _reporter.FindToolPathExternal("exiftool.exe");
            if (string.IsNullOrEmpty(toolPath) || !File.Exists(toolPath))
            {
                _reporter.LogMessage(_reporter.GetLocalizedString("ExifToolNotFound", _selectedExifToolTag) + " ExifRemoveViewModel will not function correctly.");
                _exifProcessor = null;
            }
            else
            {
                if (_exifProcessor == null || _exifProcessor.ExifToolPath != toolPath)
                {
                    _exifProcessor = new ExifProcessor(toolPath);
                }
            }
        }

        public void LoadSettings(NamingOptions globalNamingOptions, PathOptions customPathOptions, bool generalEnableBackup, int originalFileActionIndex, string outputImageFormat, int jpegQuality, string selectedExifToolTag)
        {
            _globalNamingOptions = globalNamingOptions;
            _customPathOptions = customPathOptions;
            _generalEnableBackup = generalEnableBackup;
            _originalFileActionIndex = originalFileActionIndex;
            _selectedExifToolTag = selectedExifToolTag;
            InitializeExifProcessor();
        }

        public async Task ExecuteAsync(List<string> inputPaths, CancellationToken token, IUIReporter reporter)
        {
            if (_exifProcessor == null || !File.Exists(_exifProcessor.ExifToolPath))
            {
                reporter.ShowMessage(reporter.GetLocalizedString("ExifToolNotFound", _selectedExifToolTag), reporter.GetLocalizedString("ErrorTitle"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                reporter.LogMessage(reporter.GetLocalizedString("ExifToolNotFound", _selectedExifToolTag));
                return;
            }

            reporter.LogMessage(reporter.GetLocalizedString("ExifMode_StartLog"));
            reporter.UpdateStatusLabel(reporter.GetLocalizedString("ProcessingReady"));
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

            int totalImages = filesToProcessDetails.Count;
            int processedCountInternal = 0;
            int failedCountInternal = 0;
            reporter.UpdateCounts(processedCountInternal, failedCountInternal, totalImages);
            reporter.UpdateProgressBar(0, totalImages, false);
            reporter.LogMessage(reporter.GetLocalizedString("StartingProcessing", totalImages));

            var removalOptions = GetRemovalOptions();
            var timestampForRun = _mainViewModel.GetValidatedTimestampString(_globalNamingOptions.TimestampFormat, DateTime.Now);

            var processingTasks = new List<Task>();
            var renamingCounters = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            bool customOutputRequiresSubdirs = _customPathOptions.UseCustomImageOutputPath && !string.IsNullOrWhiteSpace(_customPathOptions.CustomImageOutputPath) &&
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

                    string? finalOutputFilePath = null;
                    Stopwatch sw = Stopwatch.StartNew();
                    string fileDisplayName = Path.GetFileName(originalFilePath);
                    bool taskSuccess = false;

                    try
                    {
                        token.ThrowIfCancellationRequested();
                        int currentItemIndexForStatus = Interlocked.Increment(ref processedCountInternal);
                        reporter.UpdateStatusLabel($"{reporter.GetLocalizedString(_generalEnableBackup ? "ProcessingFile" : "ProcessingFileNoBackup", fileDisplayName)} ({currentItemIndexForStatus + failedCountInternal}/{totalImages})");

                        string? originalInputItemForFile = _mainViewModel.FindBestInputPathForFile(originalFilePath, inputPaths, reporter);
                        if (string.IsNullOrEmpty(originalInputItemForFile))
                        {
                            throw new InvalidOperationException(reporter.GetLocalizedString("ErrorNoInputContext", originalFilePath));
                        }

                        string relativePathDir = "";
                        try
                        {
                            relativePathDir = Path.GetRelativePath(originalInputItemForFile, originalFilePath);
                            relativePathDir = Path.GetDirectoryName(relativePathDir)?.TrimStart('.', Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? "";
                        }
                        catch (Exception ex)
                        {
                            reporter.LogMessage(reporter.GetLocalizedString("WarnRelativePath", originalFilePath, originalInputItemForFile, ex.Message));
                            relativePathDir = "";
                        }

                        string finalOutputDirectory;
                        if (_customPathOptions.UseCustomImageOutputPath && !string.IsNullOrWhiteSpace(_customPathOptions.CustomImageOutputPath))
                        {
                            finalOutputDirectory = customOutputRequiresSubdirs ?
                                Path.Combine(_customPathOptions.CustomImageOutputPath, Path.GetFileName(originalInputItemForFile), relativePathDir) :
                                Path.Combine(_customPathOptions.CustomImageOutputPath, relativePathDir);
                        }
                        else
                        {
                            string? originalParentDir = Path.GetDirectoryName(originalFilePath);
                            if (string.IsNullOrEmpty(originalParentDir))
                            {
                                throw new InvalidOperationException(reporter.GetLocalizedString("ErrorDeterminingDirectory", originalFilePath));
                            }
                            finalOutputDirectory = originalParentDir;
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
                        string? generatedPath = _fileNamer.GenerateNewFilePath(
                            originalFilePath, _globalNamingOptions, counterValue, finalOutputDirectory,
                            !_mainViewModel.OverwriteOutput
                        );
                        if (generatedPath == null)
                        {
                            throw new InvalidOperationException(reporter.GetLocalizedString("ErrorUniqueFile", originalFileNameWithoutExtension ?? fileDisplayName, finalOutputDirectory));
                        }
                        finalOutputFilePath = generatedPath;

                        bool processToNewFile = _customPathOptions.UseCustomImageOutputPath || _originalFileActionIndex == 1 || _originalFileActionIndex == 2;
                        string finalDestinationForTool = processToNewFile ? finalOutputFilePath : currentProcessingFile;

                        if (processToNewFile && finalDestinationForTool != currentProcessingFile)
                        {
                            try { File.Copy(currentProcessingFile, finalDestinationForTool, !_mainViewModel.OverwriteOutput); }
                            catch (IOException ioEx) when (!_mainViewModel.OverwriteOutput && ioEx is System.IO.IOException && ioEx.HResult == -2147024816)
                            {
                                Interlocked.Decrement(ref processedCountInternal);
                                reporter.LogMessage($"Skipping '{fileDisplayName}' because target output '{finalDestinationForTool}' exists and overwrite is disabled.");
                                throw new OperationCanceledException("Skipped due to target existing.");
                            }
                            catch (Exception copyEx) { throw new IOException($"Error copying '{currentProcessingFile}' to '{finalDestinationForTool}' for processing: {copyEx.Message}", copyEx); }
                        }

                        var result = await _exifProcessor!.RemoveExifAsync(
                            finalDestinationForTool, finalDestinationForTool, removalOptions, null, token
                        );
                        sw.Stop();

                        if (result.Success)
                        {
                            taskSuccess = true;
                            reporter.LogMessage(reporter.GetLocalizedString("SuccessRename", fileDisplayName, Path.GetFileName(result.NewFilePath ?? finalOutputFilePath)) + $" ({sw.Elapsed.TotalSeconds:F2}s)");
                            if (processToNewFile && _originalFileActionIndex == 2)
                            {
                                string originalToDelete = originalFilePath;
                                reporter.LogMessage(reporter.GetLocalizedString("DeletingOriginalAfterSuccess", originalToDelete));
                                try { if (File.Exists(originalToDelete)) File.Delete(originalToDelete); }
                                catch (Exception delEx) { reporter.LogMessage(reporter.GetLocalizedString("ErrorDeletingOriginal", originalToDelete, delEx.Message)); }
                            }
                        }
                        else
                        {
                            reporter.LogMessage($"EXIF REMOVE FAIL: {fileDisplayName}. {result.ErrorMessage ?? "Unknown error."} (Code {result.ExitCode}) ({sw.Elapsed.TotalSeconds:F2}s)");
                        }
                    }
                    catch (OperationCanceledException opEx)
                    {
                        if (opEx.Message != "Skipped due to target existing.")
                        {
                            Interlocked.Decrement(ref processedCountInternal);
                        }
                        else
                        {
                            taskSuccess = false;
                        }
                        reporter.LogMessage(reporter.GetLocalizedString("Debug_TaskCancelledGeneric", fileDisplayName));
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Decrement(ref processedCountInternal);
                        reporter.LogMessage(reporter.GetLocalizedString("UnexpectedErrorProcessingFile", originalFilePath, ex.GetType().Name, ex.Message));
                        sw.Stop();
                        if (finalOutputFilePath != null && File.Exists(finalOutputFilePath) && finalOutputFilePath != currentProcessingFile)
                        {
                            try { File.Delete(finalOutputFilePath); } catch { }
                        }
                    }
                    finally
                    {
                        if (!taskSuccess && !(token.IsCancellationRequested))
                        {
                            Interlocked.Increment(ref failedCountInternal);
                        }
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
            catch (OperationCanceledException)
            {
                reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllCaughtCancellation"));
            }
            catch (Exception ex)
            {
                reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllCaughtError", ex.GetType().Name, ex.Message));
                if (ex is AggregateException aggEx)
                {
                    foreach (var innerEx in aggEx.Flatten().InnerExceptions)
                    {
                        reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllInnerError", innerEx.GetType().Name, innerEx.Message));
                    }
                }
            }
        }

        public ExifRemovalOptions GetRemovalOptions()
        {
            return new ExifRemovalOptions(
                RemoveAllMetadata: this.RemoveAllMetadata,
                KeepDateTaken: this.KeepDateTaken,
                KeepGps: this.KeepGps,
                KeepOrientation: this.KeepOrientation,
                KeepCameraInfo: this.KeepCameraInfo,
                KeepColorSpace: this.KeepColorSpace,
                RemoveThumbnail: this.RemoveThumbnail
            );
        }
    }
}