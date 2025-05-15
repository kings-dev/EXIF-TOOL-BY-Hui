// ViewModels/UIReporter.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Hui_WPF.Models;
using Hui_WPF.Utils;
using Microsoft.Win32;
using Ookii.Dialogs.Wpf;

namespace Hui_WPF.ViewModels
{
    public class UIReporter : IUIReporter
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _logMessageAction;
        private readonly Action<string> _updateStatusLabelAction;
        private readonly Action<int, int?, bool> _updateProgressBarAction;
        private readonly Action<int, int, int> _updateCountsAction;
        private readonly Action<int> _updateActiveTasksAction;
        private readonly Action<string> _openFolderAction;
        private readonly Func<string, string?, string?, string, string, CancellationToken, Task<string?>> _showSaveFileDialogAction;
        private readonly Func<string, string?, CancellationToken, Task<string[]?>> _showBrowseDialogAction;
        private readonly Func<string, string?, CancellationToken, Task<string?>> _showFolderBrowserDialogAction;
        private readonly Func<string, string?, bool, CancellationToken, Task<string[]?>> _showImageFileDialogAction;
        private readonly Func<string, string?, MessageBoxButton, MessageBoxImage, MessageBoxResult> _showMessageBoxSyncAction;
        private readonly Action _applyLocalizationAction;
        private readonly Func<string, string?> _findToolPathAction;

        public int ProcessedCount { get; private set; }
        public int FailedCount { get; private set; }
        public int TotalCount { get; private set; }

        public UIReporter(Dispatcher dispatcher,
                          Action<string> logMessageAction,
                          Action<string> updateStatusLabelAction,
                          Action<int, int?, bool> updateProgressBarAction,
                          Action<int, int, int> updateCountsAction,
                          Action<int> updateActiveTasksAction,
                          Action<string> openFolderAction,
                          Func<string, string?, string?, string, string, CancellationToken, Task<string?>> showSaveFileDialogAction,
                          Func<string, string?, CancellationToken, Task<string[]?>> showBrowseDialogAction,
                          Func<string, string?, CancellationToken, Task<string?>> showFolderBrowserDialogAction,
                          Func<string, string?, bool, CancellationToken, Task<string[]?>> showImageFileDialogAction,
                          Func<string, string?, MessageBoxButton, MessageBoxImage, MessageBoxResult> showMessageBoxSyncAction,
                          Action applyLocalizationAction,
                          Func<string, string?> findToolPathAction)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _logMessageAction = logMessageAction ?? throw new ArgumentNullException(nameof(logMessageAction));
            _updateStatusLabelAction = updateStatusLabelAction ?? throw new ArgumentNullException(nameof(updateStatusLabelAction));
            _updateProgressBarAction = updateProgressBarAction ?? throw new ArgumentNullException(nameof(updateProgressBarAction));
            _updateCountsAction = updateCountsAction ?? throw new ArgumentNullException(nameof(updateCountsAction));
            _updateActiveTasksAction = updateActiveTasksAction ?? throw new ArgumentNullException(nameof(updateActiveTasksAction));
            _openFolderAction = openFolderAction ?? throw new ArgumentNullException(nameof(openFolderAction));
            _showSaveFileDialogAction = showSaveFileDialogAction ?? throw new ArgumentNullException(nameof(showSaveFileDialogAction));
            _showBrowseDialogAction = showBrowseDialogAction ?? throw new ArgumentNullException(nameof(showBrowseDialogAction));
            _showFolderBrowserDialogAction = showFolderBrowserDialogAction ?? throw new ArgumentNullException(nameof(showFolderBrowserDialogAction));
            _showImageFileDialogAction = showImageFileDialogAction ?? throw new ArgumentNullException(nameof(showImageFileDialogAction));
            _showMessageBoxSyncAction = showMessageBoxSyncAction ?? throw new ArgumentNullException(nameof(showMessageBoxSyncAction));
            _applyLocalizationAction = applyLocalizationAction ?? throw new ArgumentNullException(nameof(applyLocalizationAction));
            _findToolPathAction = findToolPathAction ?? throw new ArgumentNullException(nameof(findToolPathAction));
        }

        public void LogMessage(string message) => _dispatcher.BeginInvoke(_logMessageAction, DispatcherPriority.Background, message);
        public void UpdateStatusLabel(string text) => _dispatcher.BeginInvoke(_updateStatusLabelAction, DispatcherPriority.Background, text);
        public void UpdateProgressBar(int value, int? max = null, bool indeterminate = false) => _dispatcher.BeginInvoke(_updateProgressBarAction, DispatcherPriority.Background, value, max, indeterminate);

        public void UpdateCounts(int processed, int failed, int total)
        {
            ProcessedCount = processed; FailedCount = failed; TotalCount = total;
            _dispatcher.BeginInvoke(_updateCountsAction, DispatcherPriority.Background, processed, failed, total);
        }
        public void UpdateActiveTasks(int count) => _dispatcher.BeginInvoke(_updateActiveTasksAction, DispatcherPriority.Background, count);
        public void ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage icon) => _dispatcher.InvokeAsync(() => _showMessageBoxSyncAction(message, title, buttons, icon));

        public async Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButton buttons, MessageBoxImage icon, CancellationToken token)
        {
            if (token.IsCancellationRequested) return MessageBoxResult.Cancel;
            if (_dispatcher.CheckAccess()) return _showMessageBoxSyncAction(message, title, buttons, icon);
            else return await _dispatcher.InvokeAsync(() => _showMessageBoxSyncAction(message, title, buttons, icon), DispatcherPriority.Normal, token);
        }
        public string GetLocalizedString(string key, string? fallback = null) => LocalizationHelper.GetLocalizedString(key, fallback);
        public string GetLocalizedString(string key, params object?[]? args) => LocalizationHelper.GetLocalizedString(key, args);
        public void OpenFolderInExplorer(string path) => _dispatcher.BeginInvoke(_openFolderAction, DispatcherPriority.Background, path);
        public async Task<string[]?> ShowBrowseDialogAsync(string title, string? initialDirectory, CancellationToken token) => token.IsCancellationRequested ? null : await _showBrowseDialogAction(title, initialDirectory, token);
        public async Task<string?> ShowFolderBrowserDialogAsync(string title, string? initialDirectory, CancellationToken token) => token.IsCancellationRequested ? null : await _showFolderBrowserDialogAction(title, initialDirectory, token);
        public async Task<string[]?> ShowImageFileDialogAsync(string title, string? initialDirectory, bool multiselect, CancellationToken token) => token.IsCancellationRequested ? null : await _showImageFileDialogAction(title, initialDirectory, multiselect, token);
        public async Task<string?> ShowSaveFileDialogAsync(string title, string? initialDirectory, string? defaultFileName, string filter, string defaultExtension, CancellationToken token) => token.IsCancellationRequested ? null : await _showSaveFileDialogAction(title, initialDirectory, defaultFileName, filter, defaultExtension, token);
        public void ApplyLocalization() => _dispatcher.BeginInvoke(_applyLocalizationAction, DispatcherPriority.Background);
        public string? FindToolPathExternal(string executableName) => _findToolPathAction(executableName);
    }
}