using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace Hui_WPF.Models
{
    public enum ProcessingStatus
    {
        Pending,
        Processing,
        Success,
        Failed,
        Skipped,
        Cancelled
    }

    public class ImageItem : INotifyPropertyChanged
    {
        private string _originalFullPath = string.Empty;
        private string _currentFullPath = string.Empty;
        private string? _proposedNewPath = null;
        private ProcessingStatus _status = ProcessingStatus.Pending;
        private string? _errorMessage = null;
        private string _displayName = string.Empty;

        public string OriginalFullPath
        {
            get => _originalFullPath;
            set { if (_originalFullPath != value) { _originalFullPath = value; OnPropertyChanged(); UpdateDisplayName(); } }
        }

        public string CurrentFullPath
        {
            get => _currentFullPath;
            set { if (_currentFullPath != value) { _currentFullPath = value; OnPropertyChanged(); UpdateDisplayName(); } }
        }

        public string? ProposedNewPath
        {
            get => _proposedNewPath;
            set { if (_proposedNewPath != value) { _proposedNewPath = value; OnPropertyChanged(); } }
        }

        public ProcessingStatus Status
        {
            get => _status;
            set { if (_status != value) { _status = value; OnPropertyChanged(); } }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { if (_errorMessage != value) { _errorMessage = value; OnPropertyChanged(); } }
        }

        public string DisplayName
        {
            get => _displayName;
            private set { if (_displayName != value) { _displayName = value; OnPropertyChanged(); } }
        }

        public ImageItem(string originalPath)
        {
            OriginalFullPath = originalPath ?? throw new ArgumentNullException(nameof(originalPath));
            CurrentFullPath = originalPath;
            UpdateDisplayName();
        }

        public ImageItem() { }

        private void UpdateDisplayName()
        {
            string pathToShow = !string.IsNullOrEmpty(CurrentFullPath) ? CurrentFullPath : OriginalFullPath;
            try
            {
                DisplayName = System.IO.Path.GetFileName(pathToShow);
            }
            catch
            {
                DisplayName = pathToShow;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (PropertyChanged == null) return;
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                   PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName))
                );
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is ImageItem other)
            {
                return string.Equals(this.OriginalFullPath, other.OriginalFullPath, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(this.OriginalFullPath ?? string.Empty);
        }
    }
}