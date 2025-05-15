// ViewModels/ExifWriteViewModel.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Hui_WPF.Core;
using Hui_WPF.Models;
using Hui_WPF.Utils;

namespace Hui_WPF.ViewModels
{
    public class ExifWriteViewModel : ViewModelBase, IProcessingTaskViewModel
    {
        private readonly IUIReporter _reporter;
        private readonly FileNamer _fileNamer = new FileNamer();
        private readonly MainViewModel _mainViewModel;
        private ExifProcessor? _exifProcessor;

        private bool _writeCommonTags = false;
        public bool WriteCommonTags { get => _writeCommonTags; set { if (SetProperty(ref _writeCommonTags, value)) OnPropertyChanged(nameof(CanEditCommonTags)); } }

        private string? _artist = null;
        public string? Artist { get => _artist; set => SetProperty(ref _artist, value?.Trim()); }

        private string? _copyright = null;
        public string? Copyright { get => _copyright; set => SetProperty(ref _copyright, value?.Trim()); }

        private string? _comment = null;
        public string? Comment { get => _comment; set => SetProperty(ref _comment, value?.Trim()); }

        private string? _description = null;
        public string? Description { get => _description; set => SetProperty(ref _description, value?.Trim()); }

        private int? _rating = null;
        public int? Rating { get => _rating; set => SetProperty(ref _rating, value); }

        private bool _writeDateTaken = false;
        public bool WriteDateTaken { get => _writeDateTaken; set { if (SetProperty(ref _writeDateTaken, value)) OnPropertyChanged(nameof(CanEditDateTaken)); } }

        private DateTime? _dateTakenPart = DateTime.Today;
        public DateTime? DateTakenPart
        {
            get => _dateTakenPart;
            set { if (SetProperty(ref _dateTakenPart, value)) OnPropertyChanged(nameof(DateTimeOriginal)); }
        }

        private TimeSpan? _timeTakenPart = TimeSpan.Zero;
        public TimeSpan? TimeTakenPart // This property will be updated by HourTaken, MinuteTaken, SecondTaken setters
        {
            get => _timeTakenPart;
            private set { if (SetProperty(ref _timeTakenPart, value)) OnPropertyChanged(nameof(DateTimeOriginal)); }
        }

        public DateTime? DateTimeOriginal
        {
            get
            {
                if (!(_dateTakenPart is DateTime datePart)) return null;
                if (!(_timeTakenPart is TimeSpan timePart)) timePart = TimeSpan.Zero; // Default to midnight if time part is null
                try { return datePart.Date + timePart; }
                catch (ArgumentOutOfRangeException) { return null; }
            }
            set // This setter is useful if you want to set the entire DateTime at once
            {
                if (value.HasValue)
                {
                    DateTakenPart = value.Value.Date;
                    // Update individual time components and trigger their PropertyChanged
                    _hourTaken = value.Value.Hour.ToString("D2");
                    _minuteTaken = value.Value.Minute.ToString("D2");
                    _secondTaken = value.Value.Second.ToString("D2");
                    OnPropertyChanged(nameof(HourTaken));
                    OnPropertyChanged(nameof(MinuteTaken));
                    OnPropertyChanged(nameof(SecondTaken));
                    UpdateTimePartFromComponents(); // This will update _timeTakenPart and notify DateTimeOriginal
                }
                else
                {
                    DateTakenPart = null;
                    TimeTakenPart = null; // This will also trigger DateTimeOriginal update via its setter
                }
            }
        }

        private string _hourTaken = "12";
        public string HourTaken { get => _hourTaken; set { if (SetProperty(ref _hourTaken, value)) UpdateTimePartFromComponents(); } }

        private string _minuteTaken = "00";
        public string MinuteTaken { get => _minuteTaken; set { if (SetProperty(ref _minuteTaken, value)) UpdateTimePartFromComponents(); } }

        private string _secondTaken = "00";
        public string SecondTaken { get => _secondTaken; set { if (SetProperty(ref _secondTaken, value)) UpdateTimePartFromComponents(); } }


        private bool _writeGps = false;
        public bool WriteGps { get => _writeGps; set { if (SetProperty(ref _writeGps, value)) OnPropertyChanged(nameof(CanEditGps)); } }

        private double? _latitude = null;
        public double? Latitude { get => _latitude; set => SetProperty(ref _latitude, value); }

        private double? _longitude = null;
        public double? Longitude { get => _longitude; set => SetProperty(ref _longitude, value); }

        public bool CanEditCommonTags => WriteCommonTags;
        public bool CanEditDateTaken => WriteDateTaken;
        public bool CanEditGps => WriteGps;

        private NamingOptions _globalNamingOptions = NamingOptionsDefaults.Default;
        private PathOptions _customPathOptions = PathOptionsDefaults.Default;
        private bool _generalEnableBackup = true;
        private int _originalFileActionIndex = 0;
        private string _selectedExifToolTag = "ExifTool";


        public ExifWriteViewModel(IUIReporter reporter, MainViewModel mainViewModel)
        {
            _reporter = reporter;
            _mainViewModel = mainViewModel;
            InitializeExifProcessor();
            if (_dateTakenPart == null) _dateTakenPart = DateTime.Today; // Ensure a default date
            UpdateTimePartFromComponents(); // Initialize TimeTakenPart
        }

        private void InitializeExifProcessor()
        {
            string? toolPath = _reporter.FindToolPathExternal(_selectedExifToolTag) ?? _reporter.FindToolPathExternal("exiftool.exe");
            if (string.IsNullOrEmpty(toolPath) || !File.Exists(toolPath))
            {
                _reporter.LogMessage(_reporter.GetLocalizedString("ExifToolNotFound", _selectedExifToolTag) + " ExifWriteViewModel will not function correctly.");
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

        private void UpdateTimePartFromComponents()
        {
            if (int.TryParse(HourTaken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hour) && hour >= 0 && hour <= 23 &&
                int.TryParse(MinuteTaken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minute) && minute >= 0 && minute <= 59 &&
                int.TryParse(SecondTaken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int second) && second >= 0 && second <= 59)
            {
                TimeTakenPart = new TimeSpan(hour, minute, second);
            }
            else
            {
                TimeTakenPart = null; // Or TimeSpan.Zero if you prefer a default time
            }
        }

        public void LoadSettings(NamingOptions globalNamingOptions, PathOptions customPathOptions, bool generalEnableBackup, int originalFileActionIndex, string outputImageFormat, int jpegQuality, string selectedExifToolTag)
        {
            _globalNamingOptions = globalNamingOptions;
            _customPathOptions = customPathOptions;
            _generalEnableBackup = generalEnableBackup;
            _originalFileActionIndex = originalFileActionIndex;
            _selectedExifToolTag = selectedExifToolTag;
            // outputImageFormat and jpegQuality are not directly used by ExifWrite
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

            reporter.LogMessage(reporter.GetLocalizedString("ExifMode_StartLog") + " (Write)");
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

            var writeOptions = GetWriteOptions();
            if (!writeOptions.WriteCommonTags && !writeOptions.WriteDateTaken && !writeOptions.WriteGps)
            {
                reporter.LogMessage("No EXIF write options selected. Skipping operation.");
                reporter.UpdateStatusLabel("完成 (无写入操作)");
                reporter.UpdateProgressBar(totalImages, totalImages, false);
                reporter.UpdateCounts(totalImages, 0, totalImages); // All items are "processed" by being skipped
                return;
            }

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

                    bool processToNewFile = false; // Initialize
                    string targetProcessPath = currentProcessingFile; // Initialize
                    string? finalOutputFilePath = null;
                    ExifProcessor.ProcessingResult? result = null;

                    Stopwatch sw = Stopwatch.StartNew();
                    string fileDisplayName = Path.GetFileName(originalFilePath);
                    bool taskSuccess = false;

                    try
                    {
                        token.ThrowIfCancellationRequested();
                        int currentItemIndexForStatus = Interlocked.Increment(ref processedCountInternal);
                        reporter.UpdateStatusLabel($"{reporter.GetLocalizedString(_generalEnableBackup ? "ProcessingFile" : "ProcessingFileNoBackup", fileDisplayName)} ({currentItemIndexForStatus + failedCountInternal}/{totalImages})");

                        processToNewFile = _customPathOptions.UseCustomImageOutputPath || _mainViewModel.OriginalFileActionIndex == 1 || _mainViewModel.OriginalFileActionIndex == 2;

                        if (processToNewFile)
                        {
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
                            else // Output to original file's directory (but new file name because OriginalFileAction is 1 or 2)
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
                            // For EXIF Write, usually the filename is kept unless explicitly renaming.
                            // Here, we use global settings to generate a NEW filename if processToNewFile is true.
                            string newBaseFileName = _mainViewModel.ConstructNameFromGlobalSettings(
                               counterValue, DateTime.Now, Path.GetFileName(originalInputItemForFile), originalFileNameWithoutExtension
                            );
                            string? generatedPath = _fileNamer.GenerateNewFilePath(
                                originalFilePath, // Use original for extension and base if no prefix/ts/counter
                                _globalNamingOptions,
                                counterValue,
                                finalOutputDirectory,
                                !_mainViewModel.OverwriteOutput // Try adding suffix if overwrite disabled and collision
                            );

                            if (generatedPath == null)
                            {
                                throw new InvalidOperationException(reporter.GetLocalizedString("ErrorUniqueFile", originalFileNameWithoutExtension ?? fileDisplayName, finalOutputDirectory));
                            }
                            finalOutputFilePath = generatedPath;
                            targetProcessPath = finalOutputFilePath;

                            try
                            {
                                File.Copy(currentProcessingFile, targetProcessPath, !_mainViewModel.OverwriteOutput);
                            }
                            catch (IOException ioEx) when (!_mainViewModel.OverwriteOutput && ioEx.Message.Contains("already exists")) // More specific check for existing file
                            {
                                Interlocked.Decrement(ref processedCountInternal); // Not processed
                                reporter.LogMessage($"Skipping '{fileDisplayName}' because target output '{targetProcessPath}' exists and overwrite is disabled.");
                                throw new OperationCanceledException("Skipped due to target existing."); // Allows specific handling
                            }
                            catch (Exception copyEx) { throw new IOException($"Error copying '{currentProcessingFile}' to '{targetProcessPath}' for processing: {copyEx.Message}", copyEx); }
                        }
                        else // In-place modification
                        {
                            targetProcessPath = currentProcessingFile;
                            finalOutputFilePath = currentProcessingFile;
                        }

                        result = await _exifProcessor!.WriteExifAsync(targetProcessPath, writeOptions, null, token);
                        sw.Stop();

                        if (result.Success)
                        {
                            taskSuccess = true;
                            reporter.LogMessage(reporter.GetLocalizedString("SuccessProcessed", fileDisplayName, Path.GetFileName(result.NewFilePath ?? targetProcessPath)) + $" ({sw.Elapsed.TotalSeconds:F2}s)");
                            if (processToNewFile && _mainViewModel.OriginalFileActionIndex == 2 &&
                                !string.Equals(originalFilePath, currentProcessingFile, StringComparison.OrdinalIgnoreCase)) // Ensure we are not deleting the backup itself if originalFileActionIndex implies deleting "original" which might be the backup
                            {
                                string originalToDelete = originalFilePath; // This refers to the very first original file
                                reporter.LogMessage(reporter.GetLocalizedString("DeletingOriginalAfterSuccess", originalToDelete));
                                try { if (File.Exists(originalToDelete)) File.Delete(originalToDelete); }
                                catch (Exception delEx) { reporter.LogMessage(reporter.GetLocalizedString("ErrorDeletingOriginal", originalToDelete, delEx.Message)); }
                            }
                        }
                        else
                        {
                            reporter.LogMessage($"EXIF WRITE FAIL: {fileDisplayName}. {result.ErrorMessage ?? "Unknown error."} (Code {result.ExitCode}) ({sw.Elapsed.TotalSeconds:F2}s)");
                            if (processToNewFile && targetProcessPath != null && File.Exists(targetProcessPath) && targetProcessPath != currentProcessingFile)
                            {
                                try { File.Delete(targetProcessPath); } catch { /* best effort */ }
                            }
                        }
                    }
                    catch (OperationCanceledException opEx)
                    {
                        if (opEx.Message != "Skipped due to target existing.") // Don't decrement if it was a planned skip
                        {
                            Interlocked.Decrement(ref processedCountInternal);
                        }
                        else
                        {
                            taskSuccess = false; // Ensure it's not counted as successful if skipped
                        }
                        reporter.LogMessage(reporter.GetLocalizedString("Debug_TaskCancelledGeneric", fileDisplayName));
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Decrement(ref processedCountInternal);
                        reporter.LogMessage(reporter.GetLocalizedString("UnexpectedErrorProcessingFile", originalFilePath, ex.GetType().Name, ex.Message));
                        sw.Stop();
                        if (processToNewFile && targetProcessPath != null && File.Exists(targetProcessPath) && targetProcessPath != currentProcessingFile)
                        {
                            try { File.Delete(targetProcessPath); } catch { /* best effort */ }
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
            catch (OperationCanceledException) { reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllCaughtCancellation")); }
            catch (Exception ex)
            {
                reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllCaughtError", ex.GetType().Name, ex.Message));
                if (ex is AggregateException aggEx) { foreach (var innerEx in aggEx.Flatten().InnerExceptions) reporter.LogMessage(reporter.GetLocalizedString("Debug_WhenAllInnerError", innerEx.GetType().Name, innerEx.Message)); }
            }
        }

        public ExifWriteOptions GetWriteOptions()
        {
            return new ExifWriteOptions(
                WriteCommonTags: this.WriteCommonTags,
                Artist: this.Artist,
                Copyright: this.Copyright,
                Comment: this.Comment,
                Description: this.Description,
                Rating: this.Rating,
                WriteDateTaken: this.WriteDateTaken,
                DateTimeOriginal: this.DateTimeOriginal,
                WriteGps: this.WriteGps,
                Latitude: this.Latitude,
                Longitude: this.Longitude
            );
        }
    }
}