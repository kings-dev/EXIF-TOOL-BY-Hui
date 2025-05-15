using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hui_WPF.Core;
using Hui_WPF.Models;
using System.Windows;
using System.Diagnostics;
using System.Threading;
using System.Windows.Input;
using Hui_WPF.Utils;
using System.Text;

namespace Hui_WPF.ViewModels
{
    public class CreateDirectoryViewModel : ViewModelBase, IProcessingTaskViewModel
    {
        private readonly IUIReporter _reporter;
        private readonly MainViewModel _mainViewModel;

        private string _basePath = "";
        public string BasePath { get => _basePath; set { if (SetProperty(ref _basePath, value)) { UpdatePreview(); UpdateSourcePreview(); CommandManager.InvalidateRequerySuggested(); } } }

        private int _mainDirectoryCount = 3;
        public int MainDirectoryCount { get => _mainDirectoryCount; set { if (SetProperty(ref _mainDirectoryCount, Math.Max(0, value))) UpdatePreview(); } }

        private int _subdirectoryCount = 4;
        public int SubdirectoryCount { get => _subdirectoryCount; set { if (SetProperty(ref _subdirectoryCount, Math.Max(0, value))) UpdatePreview(); } }

        private bool _createSubdirectories = false;
        public bool CreateSubdirectories { get => _createSubdirectories; set { if (SetProperty(ref _createSubdirectories, value)) UpdatePreview(); } }

        private bool _useFolder = true;
        public bool UseFolder { get => _useFolder; set { if (SetProperty(ref _useFolder, value)) UpdatePreview(); } }

        private bool _useParent = false;
        public bool UseParent { get => _useParent; set { if (SetProperty(ref _useParent, value)) UpdatePreview(); } }

        private bool _useChild = false;
        public bool UseChild { get => _useChild; set { if (SetProperty(ref _useChild, value)) UpdatePreview(); } }

        private string _folderText = "FD";
        public string FolderText { get => _folderText; set { if (SetProperty(ref _folderText, value)) UpdatePreview(); } }

        private string _parentText = "HF";
        public string ParentText { get => _parentText; set { if (SetProperty(ref _parentText, value)) UpdatePreview(); } }

        private string _childText = "sF";
        public string ChildText { get => _childText; set { if (SetProperty(ref _childText, value)) UpdatePreview(); } }

        private string _fileText = "File";
        public string FileText { get => _fileText; set { if (SetProperty(ref _fileText, value)) UpdatePreview(); } }

        private string _timestampFormat = "yyyyMMddHHmm";
        public string TimestampFormat { get => _timestampFormat; set { if (SetProperty(ref _timestampFormat, value)) UpdatePreview(); } }

        private string _timestampValue = "202505142140";
        public string TimestampValue { get => _timestampValue; set { if (SetProperty(ref _timestampValue, value)) UpdatePreview(); } }

        private string _separator = "_";
        public string Separator { get => _separator; set { if (SetProperty(ref _separator, value)) UpdatePreview(); } }

        private int _counterStart = 1;
        public int CounterStart 
        { 
            get => _counterStart; 
            set 
            { 
                if (SetProperty(ref _counterStart, value))
                {
                    _counterStartText = value.ToString();
                    OnPropertyChanged(nameof(CounterStartText));
                    OnPropertyChanged(nameof(CounterStart));
                    UpdatePreview();
                    CommandManager.InvalidateRequerySuggested();
                }
            } 
        }

        private string _counterStartText = "1";
        public string CounterStartText
        {
            get => _counterStartText;
            set
            {
                if (SetProperty(ref _counterStartText, value))
                {
                    int newValue;
                    if (int.TryParse(value, out newValue))
                    {
                        _counterStart = newValue;
                        OnPropertyChanged(nameof(CounterStart));
                        OnPropertyChanged(nameof(CounterStartText));
                        UpdatePreview();
                        CommandManager.InvalidateRequerySuggested();
                    }
                    else
                    {
                        _counterStart = 1;
                        _counterStartText = "1";
                        OnPropertyChanged(nameof(CounterStart));
                        OnPropertyChanged(nameof(CounterStartText));
                        UpdatePreview();
                        CommandManager.InvalidateRequerySuggested();
                    }
                }
            }
        }

        private string _counterFormat = "D2";
        public string CounterFormat 
        { 
            get => _counterFormat; 
            set 
            { 
                if (SetProperty(ref _counterFormat, value))
                {
                    UpdatePreview();
                    CommandManager.InvalidateRequerySuggested();
                }
            } 
        }

        private string _previewText = "";
        public string PreviewText { get => _previewText; set => SetProperty(ref _previewText, value); }

        public ObservableCollection<TreeViewItemViewModel> PreviewNodes { get; } = new ObservableCollection<TreeViewItemViewModel>();
        public ObservableCollection<TreeViewItemViewModel> SourcePreviewNodes { get; } = new ObservableCollection<TreeViewItemViewModel>();

        public NamingOptions GlobalNamingOptions => _mainViewModel.GlobalNamingOptions;
        public string GlobalNameOrder => _mainViewModel.GlobalNameOrder;

        public ICommand SelectBasePathCommand { get; }

        public CreateDirectoryViewModel(IUIReporter reporter, MainViewModel mainViewModel)
        {
            _reporter = reporter;
            _mainViewModel = mainViewModel;

            SelectBasePathCommand = new RelayCommand(async () => await ExecuteSelectBasePathAsync(), () => !_mainViewModel.IsProcessing);

            _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
            if (_mainViewModel.GlobalNamingOptions != null)
            {
                _mainViewModel.GlobalNamingOptions.PropertyChanged += GlobalNamingOptions_PropertyChanged_ForPreview;
            }

            PropertyChanged += LocalPropertyChanged_UpdatePreview;
            UpdatePreview();
            UpdateSourcePreview();
        }

        private void LocalPropertyChanged_UpdatePreview(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BasePath) ||
                e.PropertyName == nameof(MainDirectoryCount) ||
                e.PropertyName == nameof(SubdirectoryCount) ||
                e.PropertyName == nameof(CreateSubdirectories) ||
                e.PropertyName == nameof(UseFolder) ||
                e.PropertyName == nameof(UseParent) ||
                e.PropertyName == nameof(UseChild) ||
                e.PropertyName == nameof(FolderText) ||
                e.PropertyName == nameof(ParentText) ||
                e.PropertyName == nameof(ChildText) ||
                e.PropertyName == nameof(FileText) ||
                e.PropertyName == nameof(TimestampFormat) ||
                e.PropertyName == nameof(TimestampValue) ||
                e.PropertyName == nameof(Separator) ||
                e.PropertyName == nameof(CounterStart) ||
                e.PropertyName == nameof(CounterStartText) ||
                e.PropertyName == nameof(CounterFormat) ||
                e.PropertyName == nameof(GlobalNamingOptions.CounterStartText) ||
                e.PropertyName == nameof(GlobalNamingOptions.CounterFormat))
            { 
                UpdatePreview();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.GlobalNamingOptions))
            {
                NamingOptions? oldOpts = null;
                if (e is PropertyChangedEventArgsWithValues pcewv) oldOpts = pcewv.OldValue as NamingOptions;

                if (oldOpts != null) oldOpts.PropertyChanged -= GlobalNamingOptions_PropertyChanged_ForPreview;

                if (_mainViewModel.GlobalNamingOptions != null)
                {
                    _mainViewModel.GlobalNamingOptions.PropertyChanged -= GlobalNamingOptions_PropertyChanged_ForPreview;
                    _mainViewModel.GlobalNamingOptions.PropertyChanged += GlobalNamingOptions_PropertyChanged_ForPreview;
                }
                UpdatePreview();
                CommandManager.InvalidateRequerySuggested();
            }
            else if (
                e.PropertyName == nameof(MainViewModel.GlobalNameOrder) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.FolderText) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.ParentDirText) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.SubDirText) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.IncludeFolder) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.IncludeParentDir) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.IncludeSubDir) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.TimestampFormat) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.CounterFormat) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.Separator) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.IncludeTimestamp) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.IncludeCounter) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.UseSeparator) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.CounterStartText) ||
                e.PropertyName == nameof(MainViewModel.GlobalNamingOptions.FileNameText)
            )
            {
                UpdatePreview();
                CommandManager.InvalidateRequerySuggested();
            }
            if (e.PropertyName == nameof(MainViewModel.IsProcessing))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private void GlobalNamingOptions_PropertyChanged_ForPreview(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NamingOptions.FolderText) ||
                e.PropertyName == nameof(NamingOptions.ParentDirText) ||
                e.PropertyName == nameof(NamingOptions.SubDirText) ||
                e.PropertyName == nameof(NamingOptions.IncludeFolder) ||
                e.PropertyName == nameof(NamingOptions.IncludeParentDir) ||
                e.PropertyName == nameof(NamingOptions.IncludeSubDir) ||
                e.PropertyName == nameof(NamingOptions.TimestampFormat) ||
                e.PropertyName == nameof(NamingOptions.CounterFormat) ||
                e.PropertyName == nameof(NamingOptions.Separator) ||
                e.PropertyName == nameof(NamingOptions.IncludeTimestamp) ||
                e.PropertyName == nameof(NamingOptions.IncludeCounter) ||
                e.PropertyName == nameof(NamingOptions.UseSeparator) ||
                e.PropertyName == nameof(NamingOptions.CounterStartText))
            {
                UpdatePreview();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public async Task ExecuteAsync(List<string> inputPaths, CancellationToken token, IUIReporter reporter)
        {
            if (string.IsNullOrWhiteSpace(BasePath) || !_mainViewModel.IsValidPath(BasePath))
            {
                await reporter.ShowMessageAsync(reporter.GetLocalizedString("ErrorValidBasePathRequired"), reporter.GetLocalizedString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None);
                reporter.LogMessage(reporter.GetLocalizedString("ErrorValidBasePathRequired"));
                throw new OperationCanceledException("Validation failed: BasePath is required for Create Directory.");
            }
            await GenerateAsync(reporter, token);
        }

        public void LoadSettings(NamingOptions globalNamingOptions,
                                  PathOptions customPathOptions,
                                  bool generalEnableBackup,
                                  int originalFileActionIndex,
                                  string outputImageFormat,
                                  int jpegQuality,
                                  string selectedExifToolTag)
        {
            UpdatePreview();
        }

        private async Task ExecuteSelectBasePathAsync()
        {
            string? selected = await _reporter.ShowFolderBrowserDialogAsync(
                _reporter.GetLocalizedString("SelectFolder"),
                BasePath,
                CancellationToken.None
            );
            if (selected != null)
            {
                BasePath = selected;
            }
            CommandManager.InvalidateRequerySuggested();
        }

        public async Task GenerateAsync(IUIReporter reporter, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(BasePath)) // Double check, though ExecuteAsync should have caught it
            {
                await reporter.ShowMessageAsync(reporter.GetLocalizedString("ErrorValidBasePathRequired"), reporter.GetLocalizedString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None);
                return;
            }
            if (!Directory.Exists(BasePath))
            {
                MessageBoxResult createDirResult = await reporter.ShowMessageAsync(string.Format(reporter.GetLocalizedString("MsgConfirmCreateRoot"), BasePath), reporter.GetLocalizedString("TitleConfirmCreateRoot"), MessageBoxButton.YesNo, MessageBoxImage.Question, token);
                if (createDirResult == MessageBoxResult.Yes)
                { 
                    try 
                    { 
                        Directory.CreateDirectory(BasePath); 
                        reporter.LogMessage(reporter.GetLocalizedString("CreatedOutputDir", BasePath)); 
                    } 
                    catch (Exception ex) 
                    { 
                        await reporter.ShowMessageAsync(string.Format(reporter.GetLocalizedString("ErrorCreatingRoot", ex.Message), ex.Message), reporter.GetLocalizedString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None); 
                        reporter.LogMessage($"创建根目录失败: {ex.Message}"); 
                        return; 
                    } 
                }
                else 
                { 
                    return; 
                }
            }

            DateTime generationTime = DateTime.Now;
            int mainDirsCreated = 0;
            int subDirsCreated = 0;

            int totalExpectedDirs = MainDirectoryCount + (CreateSubdirectories ? MainDirectoryCount * SubdirectoryCount : 0);
            if (MainDirectoryCount == 0 && CreateSubdirectories && SubdirectoryCount > 0) // Case: only direct subdirectories
            {
                totalExpectedDirs = SubdirectoryCount;
            }
            int progressBarMax = totalExpectedDirs > 0 ? totalExpectedDirs : 1;

            reporter.UpdateStatusLabel("开始生成目录...");
            reporter.LogMessage("开始生成目录...");
            reporter.UpdateProgressBar(0, progressBarMax, false);

            try
            {
                if (string.IsNullOrWhiteSpace(_mainViewModel.GlobalNameOrder))
                {
                    _mainViewModel.GlobalNameOrder = "夹/父/子/名+时间戳+计数器";
                }

                if (MainDirectoryCount == 0 && CreateSubdirectories && SubdirectoryCount > 0)
                {
                    string subDirBaseText = DetermineSubdirectoryBaseTextForDirectSub();
                    for (int j = 1; j <= SubdirectoryCount; j++)
                    {
                        token.ThrowIfCancellationRequested();
                        string defaultBase = string.IsNullOrWhiteSpace(subDirBaseText) ? string.Empty : subDirBaseText;
                        string? explicitOverride = string.IsNullOrWhiteSpace(subDirBaseText) ? null : subDirBaseText;
                        string subDirName = _mainViewModel.ConstructNameFromGlobalSettings(
                            j,
                            generationTime,
                            defaultBaseNameForFolderComponent: defaultBase,
                            originalFileNameWithoutExtension: null,
                            explicitFolderNameOverride: explicitOverride,
                            isSubDir: true
                        );
                        if (string.IsNullOrWhiteSpace(subDirName)) subDirName = $"Sub_{j:D2}";
                        string finalSubDirPath = Path.Combine(BasePath, DirectoryRule.CleanPathSegment(subDirName));
                        CreateDirectoryIfNotExists(finalSubDirPath, reporter, ref subDirsCreated);
                        reporter.UpdateProgressBar(mainDirsCreated + subDirsCreated, progressBarMax, false);
                        reporter.UpdateStatusLabel($"创建子目录 {j}/{SubdirectoryCount}");
                        reporter.UpdateCounts(mainDirsCreated + subDirsCreated, 0, totalExpectedDirs);
                    }
                }
                else
                {
                    for (int i = 1; i <= MainDirectoryCount; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        string mainDirName = _mainViewModel.ConstructNameFromGlobalSettings(i, generationTime, defaultBaseNameForFolderComponent: "主目录", isSubDir: false);
                        if (string.IsNullOrWhiteSpace(mainDirName)) mainDirName = $"DefaultMainDir_{i:D2}";
                        string mainDirPath = Path.Combine(BasePath, DirectoryRule.CleanPathSegment(mainDirName));
                        CreateDirectoryIfNotExists(mainDirPath, reporter, ref mainDirsCreated);
                        reporter.UpdateProgressBar(mainDirsCreated + subDirsCreated, progressBarMax, false);
                        reporter.UpdateStatusLabel($"创建主目录 {i}/{MainDirectoryCount}");
                        reporter.UpdateCounts(mainDirsCreated + subDirsCreated, 0, totalExpectedDirs);

                        if (CreateSubdirectories && SubdirectoryCount > 0)
                        {
                            string subDirBaseText = DetermineSubdirectoryBaseTextForNestedSub(mainDirName);
                            for (int j = 1; j <= SubdirectoryCount; j++)
                            {
                                token.ThrowIfCancellationRequested();
                                string defaultBase2 = string.IsNullOrWhiteSpace(subDirBaseText) ? string.Empty : subDirBaseText;
                                string? explicitOverride2 = string.IsNullOrWhiteSpace(subDirBaseText) ? null : subDirBaseText;
                                string subDirName = _mainViewModel.ConstructNameFromGlobalSettings(
                                    j,
                                    generationTime,
                                    defaultBaseNameForFolderComponent: defaultBase2,
                                    originalFileNameWithoutExtension: null,
                                    explicitFolderNameOverride: explicitOverride2,
                                    isSubDir: true
                                );
                                if (string.IsNullOrWhiteSpace(subDirName)) subDirName = $"Sub_{j:D2}";
                                string subDirPath = Path.Combine(mainDirPath, DirectoryRule.CleanPathSegment(subDirName));
                                CreateDirectoryIfNotExists(subDirPath, reporter, ref subDirsCreated);
                                reporter.UpdateProgressBar(mainDirsCreated + subDirsCreated, progressBarMax, false);
                                reporter.UpdateStatusLabel($"创建主目录 {i}/{MainDirectoryCount}, 子目录 {j}/{SubdirectoryCount}");
                                reporter.UpdateCounts(mainDirsCreated + subDirsCreated, 0, totalExpectedDirs);
                            }
                        }
                    }
                }

                string successMsg = reporter.GetLocalizedString("SuccessGeneration", mainDirsCreated, subDirsCreated);
                reporter.LogMessage(successMsg);
                reporter.UpdateStatusLabel("完成");
                reporter.UpdateCounts(mainDirsCreated + subDirsCreated, 0, totalExpectedDirs);
                reporter.OpenFolderInExplorer(BasePath);
            }
            catch (OperationCanceledException)
            {
                reporter.LogMessage("目录创建已取消。");
                reporter.UpdateStatusLabel("已取消");
                reporter.UpdateCounts(mainDirsCreated + subDirsCreated, 0, totalExpectedDirs);
            }
            catch (UnauthorizedAccessException ex)
            {
                await reporter.ShowMessageAsync(ex.Message, reporter.GetLocalizedString("ErrorAuthTitle"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None);
                reporter.LogMessage(ex.Message);
                reporter.UpdateStatusLabel("错误");
                reporter.UpdateCounts(mainDirsCreated + subDirsCreated, 1, totalExpectedDirs);
            }
            catch (IOException ex)
            {
                await reporter.ShowMessageAsync(ex.Message, reporter.GetLocalizedString("ErrorIOTitle"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None);
                reporter.LogMessage(ex.Message);
                reporter.UpdateStatusLabel("错误");
                reporter.UpdateCounts(mainDirsCreated + subDirsCreated, 1, totalExpectedDirs);
            }
            catch (Exception ex)
            {
                await reporter.ShowMessageAsync(ex.Message, reporter.GetLocalizedString("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error, CancellationToken.None);
                reporter.LogMessage($"未知错误: {ex.Message}");
                reporter.UpdateStatusLabel("错误");
                reporter.UpdateCounts(mainDirsCreated + subDirsCreated, 1, totalExpectedDirs);
            }
            finally
            {
                CommandManager.InvalidateRequerySuggested();
                UpdatePreview();
            }
        }

        private void CreateDirectoryIfNotExists(string path, IUIReporter reporter, ref int countVariable)
        {
            if (!Directory.Exists(path))
            {
                try { Directory.CreateDirectory(path); countVariable++; reporter.LogMessage($"创建目录: {path}"); }
                catch (UnauthorizedAccessException ex) { throw new UnauthorizedAccessException($"权限不足创建目录 '{path}': {ex.Message}", ex); }
                catch (IOException ex) { throw new IOException($"IO错误创建目录 '{path}': {ex.Message}", ex); }
                catch (Exception ex) { throw new Exception($"意外错误创建目录 '{path}': {ex.Message}", ex); }
            }
        }

        public void UpdatePreview()
        {
            PreviewNodes.Clear();
            var rootNode = new TreeViewItemViewModel
            {
                Header = string.IsNullOrWhiteSpace(BasePath) ? "根目录" : Path.GetFileName(BasePath),
                IsExpanded = true
            };
            // 显示源目录下的现有文件和文件夹，不隐藏
            if (Directory.Exists(BasePath))
            {
                try
                {
                    foreach (var dir in Directory.GetDirectories(BasePath))
                    {
                        rootNode.Children.Add(new TreeViewItemViewModel
                        {
                            Header = Path.GetFileName(dir),
                            IsExpanded = false
                        });
                    }
                    foreach (var file in Directory.GetFiles(BasePath))
                    {
                        rootNode.Children.Add(new TreeViewItemViewModel
                        {
                            Header = Path.GetFileName(file),
                            IsExpanded = false
                        });
                    }
                }
                catch { }
            }

            var mainFolders = GenerateMainFolders();
            foreach (var mainFolder in mainFolders)
            {
                var mainNode = new TreeViewItemViewModel
                {
                    Header = mainFolder,
                    IsExpanded = true
                };

                if (CreateSubdirectories)
                {
                    var childFolders = GenerateChildFolders();
                    foreach (var childFolder in childFolders)
                    {
                        mainNode.Children.Add(new TreeViewItemViewModel
                        {
                            Header = childFolder,
                            IsExpanded = true
                        });
                    }
                }

                rootNode.Children.Add(mainNode);
            }

            PreviewNodes.Add(rootNode);
            OnPropertyChanged(nameof(PreviewNodes));
            CommandManager.InvalidateRequerySuggested();
        }

        private string GetMainName()
        {
            if (UseFolder) return FolderText;
            if (UseParent) return ParentText;
            if (UseChild) return ChildText;
            return "";
        }

        private List<string> GenerateMainFolders()
        {
            var result = new List<string>();
            DateTime now = DateTime.Now;
            
            for (int i = 0; i < MainDirectoryCount; i++)
            {
                int currentCounter = GlobalNamingOptions.CounterStartValue + i;
                string name = GetNameByPriority(now, currentCounter, isMainFolder: true);
                if (string.IsNullOrEmpty(name))
                {
                    try
                    {
                        name = $"主目录_{currentCounter.ToString(GlobalNamingOptions.CounterFormat)}";
                    }
                    catch
                    {
                        name = $"主目录_{currentCounter:D2}";
                    }
                }
                result.Add(name);
            }
            return result;
        }

        private List<string> GenerateChildFolders()
        {
            var result = new List<string>();
            DateTime now = DateTime.Now;
            
            for (int i = 0; i < SubdirectoryCount; i++)
            {
                int currentCounter = GlobalNamingOptions.CounterStartValue + i;
                string name = GetNameByPriority(now, currentCounter, isMainFolder: false);
                if (string.IsNullOrEmpty(name))
                {
                    try
                    {
                        name = $"子目录_{currentCounter.ToString(GlobalNamingOptions.CounterFormat)}";
                    }
                    catch
                    {
                        name = $"子目录_{currentCounter:D2}";
                    }
                }
                result.Add(name);
            }
            return result;
        }

        private string GetNameByPriority(DateTime now, int counter, bool isMainFolder)
        {
            var opts = _mainViewModel.GlobalNamingOptions;
            bool anyDirComponent = opts.IncludeFolder || opts.IncludeParentDir || opts.IncludeSubDir;
            string defaultBase = anyDirComponent ? (isMainFolder ? "主目录" : "子目录") : string.Empty;
            return _mainViewModel.ConstructNameFromGlobalSettings(
                counter,
                now,
                defaultBaseNameForFolderComponent: defaultBase,
                originalFileNameWithoutExtension: null,
                explicitFolderNameOverride: null,
                isSubDir: !isMainFolder
            );
        }

        private string DetermineSubdirectoryBaseTextForDirectSub()
        {
            if (GlobalNamingOptions.IncludeSubDir && !string.IsNullOrWhiteSpace(GlobalNamingOptions.SubDirText)) return GlobalNamingOptions.SubDirText.Trim();
            if (GlobalNamingOptions.IncludeFolder && !string.IsNullOrWhiteSpace(GlobalNamingOptions.FolderText)) return GlobalNamingOptions.FolderText.Trim();
            if (GlobalNamingOptions.IncludeParentDir && !string.IsNullOrWhiteSpace(GlobalNamingOptions.ParentDirText)) return GlobalNamingOptions.ParentDirText.Trim();
            return "子目录示例";
        }

        private string DetermineSubdirectoryBaseTextForNestedSub(string parentMainDirName)
        {
            if (GlobalNamingOptions.IncludeSubDir && !string.IsNullOrWhiteSpace(GlobalNamingOptions.SubDirText)) return GlobalNamingOptions.SubDirText.Trim();
            if (GlobalNamingOptions.IncludeFolder && !string.IsNullOrWhiteSpace(GlobalNamingOptions.FolderText)) return GlobalNamingOptions.FolderText.Trim();
            if (GlobalNamingOptions.IncludeParentDir && !string.IsNullOrWhiteSpace(GlobalNamingOptions.ParentDirText)) return GlobalNamingOptions.ParentDirText.Trim();

            bool parentSeemsGeneric = true;
            if (GlobalNamingOptions.IncludeFolder && !string.IsNullOrWhiteSpace(GlobalNamingOptions.FolderText) && parentMainDirName.Contains(GlobalNamingOptions.FolderText.Trim())) parentSeemsGeneric = false;
            if (GlobalNamingOptions.IncludeParentDir && !string.IsNullOrWhiteSpace(GlobalNamingOptions.ParentDirText) && parentMainDirName.Contains(GlobalNamingOptions.ParentDirText.Trim())) parentSeemsGeneric = false;

            if (parentSeemsGeneric &&
               !(GlobalNamingOptions.IncludeSubDir && !string.IsNullOrWhiteSpace(GlobalNamingOptions.SubDirText)) &&
               !(GlobalNamingOptions.IncludeFolder && !string.IsNullOrWhiteSpace(GlobalNamingOptions.FolderText)) &&
               !(GlobalNamingOptions.IncludeParentDir && !string.IsNullOrWhiteSpace(GlobalNamingOptions.ParentDirText))
              )
            {
                return ""; // Scenario 4: parent is TS_Count, child should be TS_Count
            }

            return parentMainDirName;
        }

        public void UpdateSourcePreview()
        {
            SourcePreviewNodes.Clear();
            if (Directory.Exists(BasePath))
            {
                var root = new TreeViewItemViewModel
                {
                    Header = string.IsNullOrWhiteSpace(BasePath) ? "根目录" : Path.GetFileName(BasePath),
                    FullPath = BasePath,
                    IsExpanded = true
                };
                AddDirectoryNodes(BasePath, root);
                SourcePreviewNodes.Add(root);
            }
            OnPropertyChanged(nameof(SourcePreviewNodes));
        }

        private void AddDirectoryNodes(string dir, TreeViewItemViewModel parent)
        {
            try
            {
                foreach (var subdir in Directory.GetDirectories(dir))
                {
                    var node = new TreeViewItemViewModel
                    {
                        Header = Path.GetFileName(subdir),
                        FullPath = subdir,
                        IsExpanded = false
                    };
                    parent.Children.Add(node);
                    AddDirectoryNodes(subdir, node);
                }
                foreach (var file in Directory.GetFiles(dir))
                {
                    parent.Children.Add(new TreeViewItemViewModel { Header = Path.GetFileName(file), FullPath = file });
                }
            }
            catch { }
        }
    }

    public class TreeViewItemViewModel : ViewModelBase
    {
        private string? _header;
        private bool _isExpanded;
        private bool _isError;
        public string? FullPath { get; set; }
        public string? Header { get => _header; set => SetProperty(ref _header, value); }
        public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
        public bool IsError { get => _isError; set => SetProperty(ref _isError, value); }
        public ObservableCollection<TreeViewItemViewModel> Children { get; } = new ObservableCollection<TreeViewItemViewModel>();
    }

    public class PropertyChangedEventArgsWithValues : PropertyChangedEventArgs
    {
        public virtual object? OldValue { get; }
        public virtual object? NewValue { get; }

        public PropertyChangedEventArgsWithValues(string propertyName, object? oldValue, object? newValue) : base(propertyName)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}