using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace Hui_WPF.Models
{
    public class NamingOptions : INotifyPropertyChanged
    {
        private string _prefix = "";
        public string Prefix { get => _prefix; set => SetProperty(ref _prefix, value ?? ""); }

        private bool _includeFolder = true;
        public bool IncludeFolder { get => _includeFolder; set => SetProperty(ref _includeFolder, value); }

        private bool _includeParentDir = true;
        public bool IncludeParentDir { get => _includeParentDir; set => SetProperty(ref _includeParentDir, value); }

        private bool _includeSubDir = true;
        public bool IncludeSubDir { get => _includeSubDir; set => SetProperty(ref _includeSubDir, value); }

        private bool _includeFileName = true;
        public bool IncludeFileName { get => _includeFileName; set => SetProperty(ref _includeFileName, value); }

        private string _folderText = "FD";
        public string FolderText { get => _folderText; set => SetProperty(ref _folderText, value ?? ""); }

        private string _parentDirText = "HF";
        public string ParentDirText { get => _parentDirText; set => SetProperty(ref _parentDirText, value ?? ""); }

        private string _subDirText = "sF";
        public string SubDirText { get => _subDirText; set => SetProperty(ref _subDirText, value ?? ""); }

        private string _fileNameText = "File";
        public string FileNameText { get => _fileNameText; set => SetProperty(ref _fileNameText, value ?? ""); }

        private bool _includeTimestamp = true;
        public bool IncludeTimestamp { get => _includeTimestamp; set => SetProperty(ref _includeTimestamp, value); }

        private string _timestampFormat = NamingOptionsDefaults.TimestampFormat;
        public string TimestampFormat { get => _timestampFormat; set => SetProperty(ref _timestampFormat, value ?? ""); }

        private bool _includeCounter = true;
        public bool IncludeCounter { get => _includeCounter; set => SetProperty(ref _includeCounter, value); }

        private string _counterFormat = NamingOptionsDefaults.CounterFormat;
        public string CounterFormat 
        { 
            get => _counterFormat; 
            set 
            {
                if (SetProperty(ref _counterFormat, value ?? ""))
                {
                    OnPropertyChanged(nameof(CounterFormat));
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
                    if (IsChineseNumber(value))
                    {
                        CounterStartValue = ChineseToNumber(value);
                    }
                    else if (int.TryParse(value, out int number))
                    {
                        CounterStartValue = number;
                    }
                }
            }
        }

        private int _counterStartValue = NamingOptionsDefaults.CounterStartValue;
        public int CounterStartValue 
        { 
            get => _counterStartValue; 
            set => SetProperty(ref _counterStartValue, value > 0 ? value : 1); 
        }

        private bool _useSeparator = true;
        public bool UseSeparator { get => _useSeparator; set => SetProperty(ref _useSeparator, value); }

        private string _separator = "_";
        public string Separator { get => _separator; set => SetProperty(ref _separator, value ?? ""); }

        private string _outputSubfolder = "Processed"; // Added from PathOptions to keep naming-related output structure here.
        public string OutputSubfolder { get => _outputSubfolder; set => SetProperty(ref _outputSubfolder, value ?? "Processed"); }

        private bool IsChineseNumber(string input)
        {
            string[] chineseNumbers = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            return chineseNumbers.Contains(input.Trim());
        }

        private int ChineseToNumber(string chinese)
        {
            Dictionary<string, int> chineseToNumber = new Dictionary<string, int>
            {
                { "零", 0 }, { "一", 1 }, { "二", 2 }, { "三", 3 }, { "四", 4 },
                { "五", 5 }, { "六", 6 }, { "七", 7 }, { "八", 8 }, { "九", 9 }, { "十", 10 }
            };
            return chineseToNumber.TryGetValue(chinese.Trim(), out int number) ? number : 1;
        }

        private string NumberToChinese(int number)
        {
            string[] chineseNumbers = { "零", "一", "二", "三", "四", "五", "六", "七", "八", "九", "十" };
            if (number <= 10) return chineseNumbers[number];
            if (number < 20) return "十" + (number % 10 == 0 ? "" : chineseNumbers[number % 10]);
            if (number < 100) return chineseNumbers[number / 10] + "十" + (number % 10 == 0 ? "" : chineseNumbers[number % 10]);
            return number.ToString();
        }

        public NamingOptions() { }

        public NamingOptions(NamingOptions other)
        {
            _prefix = other.Prefix;
            _includeFolder = other.IncludeFolder;
            _folderText = other.FolderText;
            _includeParentDir = other.IncludeParentDir;
            _parentDirText = other.ParentDirText;
            _includeSubDir = other.IncludeSubDir;
            _subDirText = other.SubDirText;
            _includeFileName = other.IncludeFileName;
            _fileNameText = other.FileNameText;
            _includeTimestamp = other.IncludeTimestamp;
            _timestampFormat = other.TimestampFormat;
            _includeCounter = other.IncludeCounter;
            _counterFormat = other.CounterFormat;
            _counterStartText = other.CounterStartText;
            _useSeparator = other.UseSeparator;
            _separator = other.Separator;
            _outputSubfolder = other.OutputSubfolder;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public static class NamingOptionsDefaults
    {
        public const string TimestampFormat = "yyyyMMdd_HHmmss_";
        public const string CounterFormat = "D2";
        public const int CounterStartValue = 1;

        public static NamingOptions Default => new NamingOptions
        {
            Prefix = "",
            IncludeFolder = true,
            FolderText = "FD",
            IncludeParentDir = true,
            ParentDirText = "HF",
            IncludeSubDir = true,
            SubDirText = "sF",
            IncludeFileName = true,
            FileNameText = "File",
            IncludeTimestamp = true,
            TimestampFormat = TimestampFormat,
            IncludeCounter = true,
            CounterFormat = CounterFormat,
            CounterStartText = "1",
            UseSeparator = true,
            Separator = "_",
            OutputSubfolder = "Processed"
        };
    }

    public class PathOptions : INotifyPropertyChanged
    {
        private bool _useCustomImageOutputPath = false;
        public bool UseCustomImageOutputPath { get => _useCustomImageOutputPath; set => SetProperty(ref _useCustomImageOutputPath, value); }

        private string _customImageOutputPath = "";
        public string CustomImageOutputPath { get => _customImageOutputPath; set => SetProperty(ref _customImageOutputPath, value ?? ""); }

        private bool _useCustomVideoOutputPath = false;
        public bool UseCustomVideoOutputPath { get => _useCustomVideoOutputPath; set => SetProperty(ref _useCustomVideoOutputPath, value); }

        private string _customVideoOutputPath = "";
        public string CustomVideoOutputPath { get => _customVideoOutputPath; set => SetProperty(ref _customVideoOutputPath, value ?? ""); }

        private bool _useCustomBackupPath = false;
        public bool UseCustomBackupPath { get => _useCustomBackupPath; set => SetProperty(ref _useCustomBackupPath, value); }

        private string _customBackupPath = "";
        public string CustomBackupPath { get => _customBackupPath; set => SetProperty(ref _customBackupPath, value ?? ""); }

        public PathOptions() { }

        public PathOptions(PathOptions other)
        {
            _useCustomImageOutputPath = other.UseCustomImageOutputPath;
            _customImageOutputPath = other.CustomImageOutputPath;
            _useCustomVideoOutputPath = other.UseCustomVideoOutputPath;
            _customVideoOutputPath = other.CustomVideoOutputPath;
            _useCustomBackupPath = other.UseCustomBackupPath;
            _customBackupPath = other.CustomBackupPath;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected virtual bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    public static class PathOptionsDefaults
    {
        public static PathOptions Default => new PathOptions();
    }
}