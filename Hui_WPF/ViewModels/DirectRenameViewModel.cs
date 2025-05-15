// ViewModels/DirectRenameViewModel.cs
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
using System.Windows;
using System.ComponentModel;  // Add to support INotifyPropertyChanged events for global naming options
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Hui_WPF.ViewModels
{
    public class DirectRenameViewModel : ViewModelBase, IProcessingTaskViewModel
    {
        private readonly IUIReporter _reporter;
        private readonly FileNamer _fileNamer;
        private readonly MainViewModel _mainViewModel;

        private bool _renameFiles = false;
        /// <summary>Only RenameFiles, RenameFolders, or RenameBoth may be true.</summary>
        public bool RenameFiles
        {
            get => _renameFiles;
            set
            {
                if (SetProperty(ref _renameFiles, value))
                {
                    if (value)
                    {
                        // Clear other modes
                        RenameFolders = false;
                        RenameBoth = false;
                        // 只针对重命名文件模式自动设置命名组件
                        if (_mainViewModel?.GlobalNamingOptions != null)
                        {
                            var opt = _mainViewModel.GlobalNamingOptions;
                            opt.IncludeFileName = true;
                            opt.IncludeTimestamp = true;
                            opt.IncludeCounter = true;
                            opt.IncludeFolder = false;
                            opt.IncludeParentDir = false;
                            opt.IncludeSubDir = false;
                        }
                    }
                    UpdateNamingPreview();
                    UpdateRenameTree();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _renameFolders = true;
        public bool RenameFolders
        {
            get => _renameFolders;
            set
            {
                if (SetProperty(ref _renameFolders, value))
                {
                    if (value)
                    {
                        // Clear other modes
                        RenameFiles = false;
                        RenameBoth = false;
                        // 默认：自定义命名组件全部打勾
                        if (_mainViewModel?.GlobalNamingOptions != null)
                        {
                            var opt = _mainViewModel.GlobalNamingOptions;
                            opt.IncludeFolder = true;
                            opt.IncludeParentDir = true;
                            opt.IncludeSubDir = true;
                            opt.IncludeFileName = true;
                            opt.IncludeTimestamp = true;
                            opt.IncludeCounter = true;
                        }
                    }
                    UpdateNamingPreview();
                    UpdateRenameTree();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private bool _renameBoth = false;
        /// <summary>
        /// When true, both files and folders are renamed using the same global settings.
        /// </summary>
        public bool RenameBoth
        {
            get => _renameBoth;
            set
            {
                if (SetProperty(ref _renameBoth, value))
                {
                    if (value)
                    {
                        // Clear other modes
                        RenameFiles = false;
                        RenameFolders = false;
                        // 默认：自定义命名组件全部打勾
                        if (_mainViewModel?.GlobalNamingOptions != null)
                        {
                            var opt = _mainViewModel.GlobalNamingOptions;
                            opt.IncludeFolder = true;
                            opt.IncludeParentDir = true;
                            opt.IncludeSubDir = true;
                            opt.IncludeFileName = true;
                            opt.IncludeTimestamp = true;
                            opt.IncludeCounter = true;
                        }
                    }
                    UpdateNamingPreview();
                    UpdateRenameTree();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private string _prefix = "Item_";
        public string Prefix { get => _prefix; set { if (SetProperty(ref _prefix, value)) UpdateNamingPreview(); } }

        private bool _includeTimestamp = true;
        public bool IncludeTimestamp { get => _includeTimestamp; set { if (SetProperty(ref _includeTimestamp, value)) UpdateNamingPreview(); } }

        private bool _includeCounter = true;
        public bool IncludeCounter { get => _includeCounter; set { if (SetProperty(ref _includeCounter, value)) UpdateNamingPreview(); } }

        private string _timestampFormat = "yyyyMMdd";
        public string TimestampFormat { get => _timestampFormat; set { if (SetProperty(ref _timestampFormat, value)) UpdateNamingPreview(); } }

        private int _counterStartValue = 1;
        public int CounterStartValue { get => _counterStartValue; set { if (SetProperty(ref _counterStartValue, value > 0 ? value : 1)) UpdateNamingPreview(); } }

        private string _counterFormat = "D3";
        public string CounterFormat { get => _counterFormat; set { if (SetProperty(ref _counterFormat, value)) UpdateNamingPreview(); } }

        private bool _tryAddSuffixOnCollision = false;
        public bool TryAddSuffixOnCollision { get => _tryAddSuffixOnCollision; set => SetProperty(ref _tryAddSuffixOnCollision, value); }

        // 是否重命名选定的根文件夹
        private bool _renameRootFolder = true;
        public bool RenameRootFolder
        {
            get => _renameRootFolder;
            set { if (SetProperty(ref _renameRootFolder, value)) { UpdateRenameTree(); } }
        }

        private string _namingPreview = "Item_20231027_001.ext";
        public string NamingPreview { get => _namingPreview; private set => SetProperty(ref _namingPreview, value); }
        private string _folderNamingPreview = string.Empty;
        public string FolderNamingPreview { get => _folderNamingPreview; private set => SetProperty(ref _folderNamingPreview, value); }
        private string _fileNamingPreview = string.Empty;
        public string FileNamingPreview { get => _fileNamingPreview; private set => SetProperty(ref _fileNamingPreview, value); }

        private Dictionary<string, string> folderRenameMap_DirectModeOnly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Preview collections
        public ObservableCollection<TreeViewItemViewModel> SourcePreviewNodes { get; } = new ObservableCollection<TreeViewItemViewModel>();
        public ObservableCollection<RenamePreviewItem> RenamePreviewItems { get; } = new ObservableCollection<RenamePreviewItem>();
        // Hierarchical rename preview nodes
        public ObservableCollection<TreeViewItemViewModel> RenamePreviewNodes { get; } = new ObservableCollection<TreeViewItemViewModel>();

        public DirectRenameViewModel(IUIReporter reporter, MainViewModel mainViewModel)
        {
            _reporter = reporter;
            _fileNamer = new FileNamer();
            _mainViewModel = mainViewModel;

            // Subscribe to input path changes for source preview and hierarchical rename preview
            _mainViewModel.InputPaths.CollectionChanged += (s, e) => { UpdateSourcePreview(); UpdateRenameTree(); };
            // Initial previews
            UpdateSourcePreview(); UpdateRenameTree();

            PropertyChanged += (s, e) =>
            {
                // Mutual exclusion: ensure only one rename mode is selected
                if (e.PropertyName == nameof(RenameFiles) && RenameFiles)
                {
                    if (RenameFolders) RenameFolders = false;
                    if (RenameBoth) RenameBoth = false;
                }
                else if (e.PropertyName == nameof(RenameFolders) && RenameFolders)
                {
                    if (RenameFiles) RenameFiles = false;
                    if (RenameBoth) RenameBoth = false;
                }
                else if (e.PropertyName == nameof(RenameBoth) && RenameBoth)
                {
                    if (RenameFiles) RenameFiles = false;
                    if (RenameFolders) RenameFolders = false;
                }
                // Update hierarchical preview when rename mode changes
                if (e.PropertyName == nameof(RenameFiles) || e.PropertyName == nameof(RenameFolders) || e.PropertyName == nameof(RenameBoth))
                {
                    UpdateRenameTree();
                }
                // Update naming preview when rename pattern changes
                if (e.PropertyName == nameof(Prefix)
                    || e.PropertyName == nameof(IncludeTimestamp)
                    || e.PropertyName == nameof(IncludeCounter)
                    || e.PropertyName == nameof(TimestampFormat)
                    || e.PropertyName == nameof(CounterStartValue)
                    || e.PropertyName == nameof(CounterFormat))
                {
                    UpdateNamingPreview();
                }
            };
            UpdateNamingPreview();
        }

        public void LoadSettings(NamingOptions globalNamingOptions, PathOptions customPathOptions, bool generalEnableBackup, int originalFileActionIndex, string outputImageFormat, int jpegQuality, string selectedExifToolTag)
        {
            // Subscribe to global naming option changes for preview updates
            if (globalNamingOptions != null)
            {
                globalNamingOptions.PropertyChanged -= GlobalNamingOptions_PropertyChanged;
                globalNamingOptions.PropertyChanged += GlobalNamingOptions_PropertyChanged;
            }
            _mainViewModel.PropertyChanged -= MainViewModel_PropertyChanged;
            _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
            UpdateNamingPreview();
            UpdateRenameTree();
        }

        public async Task ExecuteAsync(List<string> inputPaths, CancellationToken token, IUIReporter reporter)
        {
            folderRenameMap_DirectModeOnly.Clear();
            reporter.LogMessage(_reporter.GetLocalizedString("DirectRename_StartLog"));
            reporter.UpdateStatusLabel(_reporter.GetLocalizedString("DirectRename_Preparing"));
            reporter.UpdateProgressBar(0, indeterminate: true);

            var topLevelFoldersToProcess = new List<string>();
            var filesToRename = new List<string>();

            // 修正：支持 RenameBoth 模式
            bool doFiles = RenameFiles || RenameBoth;
            bool doFolders = RenameFolders || RenameBoth;

            if (!doFiles && !doFolders)
            {
                _reporter.LogMessage(_reporter.GetLocalizedString("DirectRename_NothingSelected"));
                _reporter.UpdateStatusLabel(_reporter.GetLocalizedString("DirectRename_NothingSelected"));
                reporter.UpdateProgressBar(0, indeterminate: false);
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            try
            {
                await Task.Run(async () => // Marked lambda as async for potential internal awaits
                {
                    var collectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var collectedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string path in inputPaths)
                    {
                        token.ThrowIfCancellationRequested();
                        if (string.IsNullOrWhiteSpace(path)) continue;
                        try
                        {
                            string fullPath = Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                            if (Directory.Exists(fullPath))
                            {
                                if (doFolders)
                                {
                                    // 先添加源文件夹
                                collectedFolders.Add(fullPath);
                                    
                                    // 然后添加子文件夹
                                    var dirOpts = new EnumerationOptions { 
                                        RecurseSubdirectories = true, 
                                        IgnoreInaccessible = true, 
                                        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System 
                                    };
                                    foreach (var dir in Directory.EnumerateDirectories(fullPath, "*", dirOpts))
                                    {
                                        collectedFolders.Add(dir);
                                    }
                                }
                                if (doFiles)
                                {
                                    try
                                    {
                                        var fileOpts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden | FileAttributes.System };
                                        foreach (var file in Directory.EnumerateFiles(fullPath, "*.*", fileOpts))
                                        {
                                            token.ThrowIfCancellationRequested();
                                            collectedFiles.Add(file);
                                        }
                                    }
                                    catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException || ex is DirectoryNotFoundException)
                                    {
                                        reporter.LogMessage(reporter.GetLocalizedString("WarningScanningFolder", path, ex.GetType().Name, ex.Message));
                                    }
                                }
                            }
                            else if (doFiles && File.Exists(fullPath))
                            {
                                collectedFiles.Add(fullPath);
                            }
                            else
                            {
                                reporter.LogMessage(reporter.GetLocalizedString("IgnoringInvalidPath", path));
                            }
                        }
                        catch (Exception ex)
                        {
                            reporter.LogMessage(reporter.GetLocalizedString("ErrorCheckingFolderPath", path, ex.Message));
                        }
                    }
                    topLevelFoldersToProcess.AddRange(collectedFolders.OrderBy(f => f.Length));
                    filesToRename.AddRange(collectedFiles.OrderBy(f => f));
                }, token);
            }
            catch (OperationCanceledException)
            {
                reporter.LogMessage(reporter.GetLocalizedString("ProcessingCancelled"));
                reporter.UpdateProgressBar(0, indeterminate: false);
                CommandManager.InvalidateRequerySuggested();
                return;
            }

            int collectedFolderCount = topLevelFoldersToProcess.Count;
            int collectedFileCount = filesToRename.Count;
            if (doFolders) reporter.LogMessage(reporter.GetLocalizedString("DirectRename_FoundFolders", collectedFolderCount));
            if (doFiles) reporter.LogMessage(reporter.GetLocalizedString("DirectRename_FoundFiles", collectedFileCount));
            int totalItemsToProcess = (doFolders ? collectedFolderCount : 0) + (doFiles ? collectedFileCount : 0);
            if (totalItemsToProcess == 0)
            {
                reporter.LogMessage(reporter.GetLocalizedString("DirectRename_NothingSelected"));
                reporter.UpdateStatusLabel(reporter.GetLocalizedString("DirectRename_NothingSelected"));
                reporter.UpdateProgressBar(0, indeterminate: false);
                CommandManager.InvalidateRequerySuggested();
                return;
            }
            reporter.UpdateProgressBar(0, totalItemsToProcess, false);
            reporter.UpdateCounts(0, 0, totalItemsToProcess);
            int itemsProcessedSoFar = 0; int successfulItems = 0; int failedItems = 0;
            var folderCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var fileCounters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int validatedCounterStart = _mainViewModel.GetValidatedStartValue(CounterStartValue);
            string validatedCounterFormat = _mainViewModel.GetValidatedCounterFormat(CounterFormat);
            // Use a fixed time for direct rename to match preview
            DateTime renameTime = DateTime.Now;

            // Determine which input paths are main folders (user-selected)
            var inputFoldersSet = new HashSet<string>(inputPaths.Where(p => Directory.Exists(p)), StringComparer.OrdinalIgnoreCase);

            if (doFolders && collectedFolderCount > 0)
            {
                reporter.LogMessage(reporter.GetLocalizedString("DirectRename_StartFolders"));
                foreach (string originalFolderPath in topLevelFoldersToProcess)
                {
                    token.ThrowIfCancellationRequested(); itemsProcessedSoFar++;
                    string displayFolderName = Path.GetFileName(originalFolderPath.TrimEnd(Path.DirectorySeparatorChar));
                    reporter.UpdateStatusLabel(reporter.GetLocalizedString("DirectRename_FolderStatus", displayFolderName, itemsProcessedSoFar, totalItemsToProcess));
                    reporter.UpdateProgressBar(itemsProcessedSoFar, totalItemsToProcess, false);
                    string currentActualPath = CheckIfPathWasRenamed(originalFolderPath, folderRenameMap_DirectModeOnly) ?? originalFolderPath;
                    if (!Directory.Exists(currentActualPath)) { reporter.LogMessage(reporter.GetLocalizedString("DirectRename_FolderNotFound", originalFolderPath, currentActualPath)); failedItems++; reporter.UpdateCounts(successfulItems, failedItems, totalItemsToProcess); continue; }
                    string? parentDir = Path.GetDirectoryName(currentActualPath.TrimEnd(Path.DirectorySeparatorChar));
                    if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir)) { reporter.LogMessage(reporter.GetLocalizedString("DirectRename_ParentError", currentActualPath)); failedItems++; reporter.UpdateCounts(successfulItems, failedItems, totalItemsToProcess); continue; }
                    if (!folderCounters.ContainsKey(parentDir)) folderCounters[parentDir] = validatedCounterStart - 1;
                    folderCounters[parentDir]++; int folderIndex = folderCounters[parentDir];

                    // 获取文件夹相关的名称组件
                    string folderText = _mainViewModel.GlobalNamingOptions.IncludeFolder ? (_mainViewModel.GlobalNamingOptions.FolderText?.Trim() ?? "") : "";
                    string parentDirText = _mainViewModel.GlobalNamingOptions.IncludeParentDir ? (_mainViewModel.GlobalNamingOptions.ParentDirText?.Trim() ?? "") : "";
                    string subDirText = _mainViewModel.GlobalNamingOptions.IncludeSubDir ? (_mainViewModel.GlobalNamingOptions.SubDirText?.Trim() ?? "") : "";
                    
                    // 根据优先级组合名称
                    string folderTextForNameResolution;
                    if (!string.IsNullOrWhiteSpace(folderText))
                        folderTextForNameResolution = folderText;
                    else if (!string.IsNullOrWhiteSpace(parentDirText))
                        folderTextForNameResolution = parentDirText;
                    else if (!string.IsNullOrWhiteSpace(subDirText))
                        folderTextForNameResolution = subDirText;
                    else
                        folderTextForNameResolution = string.Empty;

                    // Determine if this is a main folder or subfolder
                    bool isMainFolder = inputFoldersSet.Contains(originalFolderPath);
                    // 根据选项决定是否跳过根目录重命名
                    if (isMainFolder && !RenameRootFolder)
                    {
                        reporter.LogMessage($"跳过重命名根目录: {originalFolderPath}");
                        continue;
                    }
                    string baseFolderName = _mainViewModel.ConstructNameFromGlobalSettings(folderIndex, renameTime,
                        defaultBaseNameForFolderComponent: folderTextForNameResolution,
                        originalFileNameWithoutExtension: null,
                        isSubDir: !isMainFolder);
                    string newFolderName = Path.Combine(parentDir, baseFolderName);
                    if (TryAddSuffixOnCollision)
                    {
                        string rootPath = newFolderName;
                        int suffix = 1;
                        while (Directory.Exists(newFolderName)) newFolderName = $"{rootPath}({suffix++})";
                    }
                    int retryCount = 0; const int maxRetries = 3; const int retryDelayMs = 300; bool moveSuccess = false; Stopwatch singleItemStopwatch = Stopwatch.StartNew();
                    while (!moveSuccess && retryCount <= maxRetries && !token.IsCancellationRequested)
                    {
                        try
                        {
                            string retryMsg = retryCount > 0 ? $" ({reporter.GetLocalizedString("RetryAttempt")} {retryCount})" : "";
                            reporter.LogMessage(reporter.GetLocalizedString("DirectRename_AttemptFolder", currentActualPath, newFolderName) + retryMsg);
                            await Task.Run(() => Directory.Move(currentActualPath, newFolderName), token); moveSuccess = true;
                        }
                        catch (IOException ioEx) when (_mainViewModel.IsAccessException(ioEx))
                        {
                            retryCount++; reporter.LogMessage(reporter.GetLocalizedString("DirectRename_AccessDeniedWarning", displayFolderName));
                            if (retryCount <= maxRetries)
                            {
                                MessageBoxResult userChoice = await reporter.ShowMessageAsync(reporter.GetLocalizedString("DirectRename_RetryPromptMessage", currentActualPath), reporter.GetLocalizedString("RetryTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning, token);
                                if (userChoice == MessageBoxResult.Yes) { reporter.LogMessage(reporter.GetLocalizedString("DirectRename_RetryLog", retryDelayMs)); await Task.Delay(retryDelayMs, token); }
                                else { reporter.LogMessage(reporter.GetLocalizedString("DirectRename_UserCancelledRetry", displayFolderName)); break; }
                            }
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { reporter.LogMessage(reporter.GetLocalizedString("DirectRename_ErrorFolder", displayFolderName, Path.GetFileName(newFolderName), ex.Message)); break; }
                    }
                    singleItemStopwatch.Stop();
                    if (moveSuccess) { successfulItems++; folderRenameMap_DirectModeOnly[originalFolderPath] = newFolderName; reporter.LogMessage(reporter.GetLocalizedString("DirectRename_FolderSuccess", originalFolderPath, newFolderName) + $" (Took:{singleItemStopwatch.Elapsed.TotalSeconds:F2}s) [{successfulItems + failedItems}/{totalItemsToProcess}]"); }
                    else { failedItems++; if (retryCount > maxRetries) reporter.LogMessage(reporter.GetLocalizedString("DirectRename_MaxRetriesReached", displayFolderName) + $" [{successfulItems + failedItems}/{totalItemsToProcess}]"); }
                    reporter.UpdateCounts(successfulItems, failedItems, totalItemsToProcess);
                }
                reporter.LogMessage(reporter.GetLocalizedString("DirectRename_FolderComplete"));
            }

            if (doFiles && collectedFileCount > 0)
            {
                reporter.LogMessage(reporter.GetLocalizedString("DirectRename_StartFiles"));
                foreach (string originalFilePath in filesToRename)
                {
                    token.ThrowIfCancellationRequested();
                    if (!doFolders) itemsProcessedSoFar++;
                    string fileDisplayName = Path.GetFileName(originalFilePath);
                    reporter.UpdateStatusLabel(reporter.GetLocalizedString("DirectRename_FileStatus", fileDisplayName, itemsProcessedSoFar, totalItemsToProcess));
                    if (!doFolders) reporter.UpdateProgressBar(itemsProcessedSoFar, totalItemsToProcess, false);
                    string currentFilePath = CheckIfPathWasRenamed(originalFilePath, folderRenameMap_DirectModeOnly) ?? originalFilePath;
                    string? currentFileDir = Path.GetDirectoryName(currentFilePath);
                    if (!File.Exists(currentFilePath))
                    {
                        bool skippedDueToParentFailure = false; string? originalDir = Path.GetDirectoryName(originalFilePath);
                        if (doFolders && originalDir != null && folderRenameMap_DirectModeOnly.ContainsKey(originalDir) && !Directory.Exists(folderRenameMap_DirectModeOnly[originalDir])) { reporter.LogMessage($"Skipping file '{fileDisplayName}' because its parent folder '{originalDir}' failed or was skipped during renaming."); skippedDueToParentFailure = true; }
                        else { reporter.LogMessage(reporter.GetLocalizedString("DirectRename_FileNotFound", currentFilePath)); }
                        if (!skippedDueToParentFailure) failedItems++; reporter.UpdateCounts(successfulItems, failedItems, totalItemsToProcess); continue;
                    }
                    if (string.IsNullOrEmpty(currentFileDir) || !Directory.Exists(currentFileDir)) { reporter.LogMessage(reporter.GetLocalizedString("DirectRename_FileDirError", fileDisplayName, currentFileDir ?? "")); failedItems++; reporter.UpdateCounts(successfulItems, failedItems, totalItemsToProcess); continue; }
                    if (!fileCounters.ContainsKey(currentFileDir)) fileCounters[currentFileDir] = validatedCounterStart - 1;
                    fileCounters[currentFileDir]++; int fileIndex = fileCounters[currentFileDir];

                    // 获取文件名相关的名称组件
                    string fileNameText = _mainViewModel.GlobalNamingOptions.FileNameText?.Trim() ?? "";
                    string fileNameNoExt = Path.GetFileNameWithoutExtension(currentFilePath);
                    
                    // 根据优先级选择文件名
                    string fileNameForPreview;
                    if (!string.IsNullOrWhiteSpace(fileNameText))
                        fileNameForPreview = fileNameText;
                    else
                        fileNameForPreview = fileNameNoExt;

                    // 获取文件夹相关的名称组件
                    string folderText = _mainViewModel.GlobalNamingOptions.IncludeFolder ? (_mainViewModel.GlobalNamingOptions.FolderText?.Trim() ?? "") : "";
                    string parentDirText = _mainViewModel.GlobalNamingOptions.IncludeParentDir ? (_mainViewModel.GlobalNamingOptions.ParentDirText?.Trim() ?? "") : "";
                    string subDirText = _mainViewModel.GlobalNamingOptions.IncludeSubDir ? (_mainViewModel.GlobalNamingOptions.SubDirText?.Trim() ?? "") : "";
                    
                    // 根据优先级组合文件夹名称, 未选中任何时为空字符串
                    string folderTextForNameResolution;
                    if (!string.IsNullOrWhiteSpace(folderText))
                        folderTextForNameResolution = folderText;
                    else if (!string.IsNullOrWhiteSpace(parentDirText))
                        folderTextForNameResolution = parentDirText;
                    else if (!string.IsNullOrWhiteSpace(subDirText))
                        folderTextForNameResolution = subDirText;
                    else
                        folderTextForNameResolution = string.Empty;

                    // 仅重命名文件模式，使用文件名组件独占逻辑
                    string fileOnlyPreview = _mainViewModel.ConstructNameFromGlobalSettings(fileIndex, renameTime,
                        defaultBaseNameForFolderComponent: folderTextForNameResolution,
                        originalFileNameWithoutExtension: fileNameForPreview,
                        isSubDir: false);
                    fileOnlyPreview += Path.GetExtension(currentFilePath);
                    var fileOnlyNode = new TreeViewItemViewModel { Header = $"{fileDisplayName} → {fileOnlyPreview}", FullPath = currentFilePath, IsExpanded = true };
                    RenamePreviewNodes.Add(fileOnlyNode);
                }
                reporter.LogMessage(reporter.GetLocalizedString("DirectRename_FileComplete"));
            }
        }

        private string? CheckIfPathWasRenamed(string originalPath, Dictionary<string, string> renameMap)
        {
            if (renameMap.TryGetValue(originalPath, out string? exactMatch)) return exactMatch;
            string? currentOriginalParent = Path.GetDirectoryName(originalPath);
            string relativePart = Path.GetFileName(originalPath);
            var parts = new Stack<string>(); parts.Push(relativePart);
            while (!string.IsNullOrEmpty(currentOriginalParent))
            {
                if (_mainViewModel.ProcessingCtsToken.IsCancellationRequested) return null; // Use public token property
                if (renameMap.TryGetValue(currentOriginalParent, out string? renamedParentPath))
                {
                    try
                    {
                        string finalPath = renamedParentPath;
                        while (parts.Count > 0) { if (_mainViewModel.ProcessingCtsToken.IsCancellationRequested) return null; finalPath = Path.Combine(finalPath, parts.Pop()); }
                        return finalPath;
                    }
                    catch (ArgumentException ex) { _reporter.LogMessage(_reporter.GetLocalizedString("RelativePathError", originalPath, currentOriginalParent, renamedParentPath ?? "null", ex.Message)); return null; }
                }
                parts.Push(Path.GetFileName(currentOriginalParent)); currentOriginalParent = Path.GetDirectoryName(currentOriginalParent);
            }
            return null;
        }

        public void UpdateNamingPreview()
        {
            // Generate preview using global naming settings
            if (_mainViewModel == null || _mainViewModel.GlobalNamingOptions == null) return;
            var options = _mainViewModel.GlobalNamingOptions;
            DateTime now = DateTime.Now;
            int counterValue = options.CounterStartValue;

            // 获取 parentFolderName
            string parentFolderName = "";
            if (_mainViewModel.InputPaths.Any())
            {
                var firstPath = _mainViewModel.InputPaths.First();
                parentFolderName = Path.GetFileName(Path.GetDirectoryName(firstPath));
            }

            // 文件名优先用 FileNameText，否则用实际文件名
            string fileNameText = options.FileNameText;
            string fileNameForPreview;
            if (!string.IsNullOrWhiteSpace(fileNameText))
            {
                fileNameForPreview = fileNameText;
            }
            else if (RenameFiles && !RenameFolders && !RenameBoth && _mainViewModel.InputPaths.Any())
            {
                var firstFile = _mainViewModel.InputPaths.First();
                fileNameForPreview = Path.GetFileNameWithoutExtension(firstFile);
            }
            else
            {
                fileNameForPreview = parentFolderName;
            }

            // 获取文件夹相关的名称组件
            string folderText = options.IncludeFolder ? (options.FolderText?.Trim() ?? "") : "";
            string parentDirText = options.IncludeParentDir ? (options.ParentDirText?.Trim() ?? "") : "";
            string subDirText = options.IncludeSubDir ? (options.SubDirText?.Trim() ?? "") : "";

            // 根据优先级组合文件夹名称, 未选中任何时为空字符串
            string folderTextForNameResolution;
            if (!string.IsNullOrWhiteSpace(folderText))
                folderTextForNameResolution = folderText;
            else if (!string.IsNullOrWhiteSpace(parentDirText))
                folderTextForNameResolution = parentDirText;
            else if (!string.IsNullOrWhiteSpace(subDirText))
                folderTextForNameResolution = subDirText;
            else
                folderTextForNameResolution = string.Empty;

            // 生成文件夹示例预览（无扩展名）
            string folderPreview = _mainViewModel.ConstructNameFromGlobalSettings(
                counterValue, now,
                defaultBaseNameForFolderComponent: folderTextForNameResolution,
                originalFileNameWithoutExtension: null,
                isSubDir: false);
            // 根据重命名模式决定是否显示文件夹示例
            bool onlyFileMode = RenameFiles && !RenameFolders && !RenameBoth;
            bool onlyFolderMode = RenameFolders && !RenameFiles && !RenameBoth;
            bool bothMode = RenameBoth;
            FolderNamingPreview = (onlyFolderMode || bothMode) ? folderPreview : string.Empty;
            // 生成文件示例预览（含扩展名）
            string filePreview = _mainViewModel.ConstructNameFromGlobalSettings(
                counterValue, now,
                defaultBaseNameForFolderComponent: folderTextForNameResolution,
                originalFileNameWithoutExtension: fileNameForPreview,
                isSubDir: false);
            string fullFilePreview = filePreview + (options.IncludeCounter || options.IncludeTimestamp || options.IncludeFileName ? Path.GetExtension(".ext") : "");
            // 根据重命名模式决定是否显示文件示例
            FileNamingPreview = (onlyFileMode || bothMode) ? fullFilePreview : string.Empty;
            // 兼容旧绑定，根据模式设置主预览
            if (onlyFolderMode) NamingPreview = FolderNamingPreview;
            else NamingPreview = FileNamingPreview;
        }

        // Handle global naming changes
        private void GlobalNamingOptions_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NamingOptions.Prefix) ||
                e.PropertyName == nameof(NamingOptions.IncludeTimestamp) ||
                e.PropertyName == nameof(NamingOptions.IncludeCounter) ||
                e.PropertyName == nameof(NamingOptions.TimestampFormat) ||
                e.PropertyName == nameof(NamingOptions.CounterStartValue) ||
                e.PropertyName == nameof(NamingOptions.CounterFormat) ||
                e.PropertyName == nameof(NamingOptions.FileNameText))
            {
                UpdateNamingPreview();
                UpdateRenameTree();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.GlobalNameOrder) || e.PropertyName == nameof(MainViewModel.GlobalNamingOptions))
            {
                UpdateNamingPreview();
                UpdateRenameTree();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Generate source structure preview
        private void UpdateSourcePreview()
        {
            SourcePreviewNodes.Clear();
            foreach (var path in _mainViewModel.InputPaths)
            {
                var header = Path.GetFileName(path);
                var node = new TreeViewItemViewModel { Header = header, FullPath = path, IsExpanded = true };
                if (Directory.Exists(path)) AddDirectoryNodes(path, node);
                SourcePreviewNodes.Add(node);
            }
        }
        private void AddDirectoryNodes(string dir, TreeViewItemViewModel parent)
        {
            try
            {
                foreach (var subdir in Directory.GetDirectories(dir))
                {
                    var subnode = new TreeViewItemViewModel { Header = Path.GetFileName(subdir), FullPath = subdir, IsExpanded = true };
                    parent.Children.Add(subnode);
                    AddDirectoryNodes(subdir, subnode);
                }
                foreach (var file in Directory.GetFiles(dir))
                {
                    parent.Children.Add(new TreeViewItemViewModel { Header = Path.GetFileName(file), FullPath = file });
                }
            }
            catch { }
        }

        // Compute folder prefix based on global settings (reuse CreateDirectory logic)
        private string ComputeFolderPrefix(bool isMainFolder)
        {
            var opt = _mainViewModel.GlobalNamingOptions;
            string prefix;
            if (opt.IncludeFolder && !opt.IncludeParentDir && !opt.IncludeSubDir)
                prefix = opt.FolderText?.Trim() ?? "";
            else if (!opt.IncludeFolder && opt.IncludeParentDir && opt.IncludeSubDir)
                prefix = isMainFolder ? (opt.ParentDirText?.Trim() ?? "") : (opt.SubDirText?.Trim() ?? "");
            else if (!opt.IncludeFolder && opt.IncludeParentDir && !opt.IncludeSubDir)
                prefix = opt.ParentDirText?.Trim() ?? "";
            else if (!opt.IncludeFolder && !opt.IncludeParentDir && !opt.IncludeSubDir)
                prefix = "";
            else if (!opt.IncludeFolder && !opt.IncludeParentDir && opt.IncludeSubDir)
                prefix = opt.SubDirText?.Trim() ?? "";
            else if (opt.IncludeFolder && opt.IncludeParentDir && !opt.IncludeSubDir)
                prefix = opt.FolderText?.Trim() ?? "";
            else if (opt.IncludeFolder && !opt.IncludeParentDir && opt.IncludeSubDir)
                prefix = isMainFolder ? (opt.FolderText?.Trim() ?? "") : (opt.SubDirText?.Trim() ?? "");
            else if (opt.IncludeFolder && opt.IncludeParentDir && opt.IncludeSubDir)
                prefix = opt.FolderText?.Trim() ?? "";
            else
            {
                if (opt.IncludeFolder && !string.IsNullOrWhiteSpace(opt.FolderText)) prefix = opt.FolderText.Trim();
                else if (opt.IncludeParentDir && !string.IsNullOrWhiteSpace(opt.ParentDirText)) prefix = opt.ParentDirText.Trim();
                else if (opt.IncludeSubDir && !string.IsNullOrWhiteSpace(opt.SubDirText)) prefix = opt.SubDirText.Trim();
                else prefix = "";
            }
            return prefix;
        }

        // Build hierarchical rename preview tree
        public void UpdateRenameTree()
        {
            RenamePreviewNodes.Clear();
            if (_mainViewModel == null || _mainViewModel.GlobalNamingOptions == null) return;
            DateTime now = DateTime.Now;
            int counter = _mainViewModel.GlobalNamingOptions.CounterStartValue;
            foreach (var path in _mainViewModel.InputPaths)
            {
                BuildRenameNode(path, ref counter, now, RenamePreviewNodes, true);
            }
        }
        private void BuildRenameNode(string path, ref int counter, DateTime now, ObservableCollection<TreeViewItemViewModel> nodes, bool isMainFolder)
        {
            bool bothMode = RenameBoth;
            bool onlyFileMode = !bothMode && RenameFiles;
            bool onlyFolderMode = !bothMode && RenameFolders;

            if (Directory.Exists(path))
            {
                string folderName = Path.GetFileName(path);
                TreeViewItemViewModel folderNode;
                // 根目录重命名可选逻辑：仅在非根或已勾选时执行预览
                if ((onlyFolderMode || bothMode) && (!isMainFolder || RenameRootFolder))
                {
                    // 获取文件夹相关的名称组件
                    string folderText = _mainViewModel.GlobalNamingOptions.IncludeFolder ? (_mainViewModel.GlobalNamingOptions.FolderText?.Trim() ?? "") : "";
                    string parentDirText = _mainViewModel.GlobalNamingOptions.IncludeParentDir ? (_mainViewModel.GlobalNamingOptions.ParentDirText?.Trim() ?? "") : "";
                    string subDirText = _mainViewModel.GlobalNamingOptions.IncludeSubDir ? (_mainViewModel.GlobalNamingOptions.SubDirText?.Trim() ?? "") : "";
                    
                    // 根据优先级组合名称
                    string folderTextForNameResolution;
                    if (!string.IsNullOrWhiteSpace(folderText))
                        folderTextForNameResolution = folderText;
                    else if (!string.IsNullOrWhiteSpace(parentDirText))
                        folderTextForNameResolution = parentDirText;
                    else if (!string.IsNullOrWhiteSpace(subDirText))
                        folderTextForNameResolution = subDirText;
                    else
                        folderTextForNameResolution = folderName;

                    // 对于文件夹预览，不传原始文件名以避免文件名独占逻辑
                    string preview = _mainViewModel.ConstructNameFromGlobalSettings(counter, now,
                        defaultBaseNameForFolderComponent: folderTextForNameResolution,
                        originalFileNameWithoutExtension: null,
                        isSubDir: !isMainFolder);
                    folderNode = new TreeViewItemViewModel { Header = $"{folderName} → {preview}", FullPath = path, IsExpanded = true };
                    counter++;
                }
                else
                {
                    folderNode = new TreeViewItemViewModel { Header = folderName, FullPath = path, IsExpanded = true };
                }
                foreach (var subdir in Directory.GetDirectories(path))
                    BuildRenameNode(subdir, ref counter, now, folderNode.Children, false);
                if (onlyFileMode || bothMode)
                    foreach (var file in Directory.GetFiles(path))
                        BuildRenameNode(file, ref counter, now, folderNode.Children, false);
                nodes.Add(folderNode);
                return;
            }
            if (onlyFileMode && File.Exists(path))
            {
                string fileName = Path.GetFileName(path);
                string parentFolderName = Path.GetFileName(Path.GetDirectoryName(path));
                string fileNameText = _mainViewModel.GlobalNamingOptions.FileNameText?.Trim() ?? "";
                string fileNameNoExt = Path.GetFileNameWithoutExtension(path);
                string fileNameForPreview = !string.IsNullOrWhiteSpace(fileNameText) ? fileNameText : fileNameNoExt;
                string folderText = _mainViewModel.GlobalNamingOptions.IncludeFolder ? (_mainViewModel.GlobalNamingOptions.FolderText?.Trim() ?? "") : "";
                string parentDirText = _mainViewModel.GlobalNamingOptions.IncludeParentDir ? (_mainViewModel.GlobalNamingOptions.ParentDirText?.Trim() ?? "") : "";
                string subDirText = _mainViewModel.GlobalNamingOptions.IncludeSubDir ? (_mainViewModel.GlobalNamingOptions.SubDirText?.Trim() ?? "") : "";
                string folderTextForNameResolution = !string.IsNullOrWhiteSpace(folderText) ? folderText :
                    (!string.IsNullOrWhiteSpace(parentDirText) ? parentDirText :
                    (!string.IsNullOrWhiteSpace(subDirText) ? subDirText : parentFolderName));
                string fileOnlyName = _mainViewModel.ConstructNameFromGlobalSettings(counter, now,
                    defaultBaseNameForFolderComponent: folderTextForNameResolution,
                    originalFileNameWithoutExtension: fileNameForPreview,
                    isSubDir: false);
                fileOnlyName += Path.GetExtension(path);
                nodes.Add(new TreeViewItemViewModel { Header = $"{fileName} → {fileOnlyName}", FullPath = path, IsExpanded = true });
                counter++;
                return;
            }
            if (bothMode && File.Exists(path))
            {
                string fileName = Path.GetFileName(path);
                string originalNameNoExt = Path.GetFileNameWithoutExtension(path);
                string bothName = _mainViewModel.ConstructNameFromGlobalSettings(counter, now,
                    defaultBaseNameForFolderComponent: originalNameNoExt,
                    originalFileNameWithoutExtension: null,
                    isSubDir: false);
                bothName += Path.GetExtension(path);
                nodes.Add(new TreeViewItemViewModel { Header = $"{fileName} → {bothName}", FullPath = path, IsExpanded = true });
                counter++;
                return;
            }
        }
    }

    // Model for rename preview
    public class RenamePreviewItem
    {
        public string OriginalName { get; set; } = string.Empty;
        public string PreviewName { get; set; } = string.Empty;
    }
}