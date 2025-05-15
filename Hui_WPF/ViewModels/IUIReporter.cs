// ViewModels/IUIReporter.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows; // For MessageBoxButton, MessageBoxImage, MessageBoxResult
using Hui_WPF.Models; // For LanguageItem if needed by GetAvailableLanguages

namespace Hui_WPF.ViewModels
{
    public interface IUIReporter
    {
        void LogMessage(string message);
        void UpdateStatusLabel(string text);
        void UpdateProgressBar(int value, int? max = null, bool indeterminate = false);
        void UpdateCounts(int processed, int failed, int total);
        void UpdateActiveTasks(int count);

        int ProcessedCount { get; } // Added
        int FailedCount { get; }    // Added
        int TotalCount { get; }     // Added

        void ShowMessage(string message, string title, MessageBoxButton buttons, MessageBoxImage icon);
        Task<MessageBoxResult> ShowMessageAsync(string message, string title, MessageBoxButton buttons, MessageBoxImage icon, CancellationToken token);
        string GetLocalizedString(string key, string? fallback = null);
        string GetLocalizedString(string key, params object?[]? args);
        void OpenFolderInExplorer(string path);
        Task<string[]?> ShowBrowseDialogAsync(string title, string? initialDirectory, CancellationToken token);
        Task<string?> ShowFolderBrowserDialogAsync(string title, string? initialDirectory, CancellationToken token);
        Task<string[]?> ShowImageFileDialogAsync(string title, string? initialDirectory, bool multiselect, CancellationToken token);
        Task<string?> ShowSaveFileDialogAsync(string title, string? initialDirectory, string? defaultFileName, string filter, string defaultExtension, CancellationToken token);
        void ApplyLocalization();
        string? FindToolPathExternal(string executableName);
    }
}