// Utils/FileDialogHelper.cs
using Microsoft.Win32; // For OpenFileDialog, SaveFileDialog
using Ookii.Dialogs.Wpf; // For VistaFolderBrowserDialog
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks; // Add for async operations
using System.Windows; // Add for Window owner parameter

namespace Hui_WPF.Utils
{
    // Helper class for showing common file and folder dialogs.
    // Methods are designed to be called from the UI thread.
    public static class FileDialogHelper
    {
        // Supported image file extensions (lowercase)
        private static readonly string[] supportedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".heic", ".webp" };

        // Gets a filter string for supported image extensions for OpenFileDialog/SaveFileDialog.
        public static string GetSupportedExtensionsFilter()
        {
            return string.Join(";", supportedExtensions.Select(ext => $"*{ext}"));
        }

        // Checks if a file extension is one of the supported image types.
        public static bool IsSupportedImageExtension(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return supportedExtensions.Contains(ext);
        }


        // Shows a dialog to select one or more image files.
        // Returns selected file paths or null if cancelled.
        public static string[]? ShowImageFileDialog(Window? owner, string title, string? initialDirectory, bool multiselect)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = $"{LocalizationHelper.GetLocalizedString("SupportedImageFiles")} ({GetSupportedExtensionsFilter()})|{GetSupportedExtensionsFilter()}|{LocalizationHelper.GetLocalizedString("AllFiles")}(*.*)|*.*",
                Multiselect = multiselect,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

            return result == true ? dialog.FileNames : null;
        }

        // Shows a dialog to select one or more image files (async wrapper).
        public static Task<string[]?> ShowImageFileDialogAsync(Window? owner, string title, string? initialDirectory, bool multiselect, CancellationToken token)
        {
            // Use Task.Run to move the sync dialog call off the UI thread.
            // However, dialogs MUST be shown on the UI thread.
            // The correct approach is to invoke the dialog on the UI thread's dispatcher.
            // This method should be called from a ViewModel or Core service that has access to the Dispatcher (via IUIReporter).
            // The IUIReporter method ShowImageFileDialogAsync would call this method on the UI thread.
            // This specific implementation here is a synchronous UI operation.
            // Let's make it directly callable from the UI thread but return a Task for async pattern compliance elsewhere.
            // No, let's follow the pattern of IUIReporter and assume this is the *implementation* called by the reporter on the UI thread.
            // It should just return the result directly, as it's already on the UI thread.

            // Corrected: This method is the *implementation* for IUIReporter.ShowImageFileDialogAsync.
            // It should return the result directly as it's assumed to be invoked on the UI thread.
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = $"{LocalizationHelper.GetLocalizedString("SupportedImageFiles")} ({GetSupportedExtensionsFilter()})|{GetSupportedExtensionsFilter()}|{LocalizationHelper.GetLocalizedString("AllFiles")}(*.*)|*.*",
                Multiselect = multiselect,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            // ShowDialog is a blocking call, token is not directly used here but implies the calling async operation might be cancelled elsewhere.
            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            return Task.FromResult(result == true ? dialog.FileNames : null);
        }


        // Shows a dialog to select a single folder.
        // Returns selected folder path or null if cancelled.
        public static string? ShowFolderBrowserDialog(Window? owner, string title, string? initialDirectory)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = title,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                SelectedPath = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

            return result == true ? dialog.SelectedPath : null;
        }

        // Shows a dialog to select a single folder (async wrapper).
        // Implementation for IUIReporter.ShowFolderBrowserDialogAsync.
        public static Task<string?> ShowFolderBrowserDialogAsync(Window? owner, string title, string? initialDirectory, CancellationToken token)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                Description = title,
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                SelectedPath = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            return Task.FromResult(result == true ? dialog.SelectedPath : null);
        }


        // Shows a dialog to select files AND/OR folders (using Ookii's dialog).
        // Note: VistaFolderBrowserDialog is only for folders. OpenFileDialog is only for files.
        // Ookii's TaskDialog can *look* like a file dialog but needs more setup.
        // The original code used FileDialogHelper.ShowBrowseDialog which returned string[].
        // This suggests a custom dialog or a workaround might have been intended for selecting both.
        // As a workaround for browsing files OR folders: Show FolderBrowserDialog first, if cancelled, show OpenFileDialog.
        // Or, use VistaOpenFileDialog from Ookii which supports selecting files OR folders if configured.

        // Let's assume ShowBrowseDialog should allow selecting MULTIPLE files AND MULTIPLE folders.
        // VistaOpenFileDialog supports MultiSelect but still primarily for files, with a 'Folders' view mode.
        // A more robust solution might involve a custom WPF window with a TreeView/ListView.
        // For this refactor, let's use VistaOpenFileDialog as it's the closest built-in option supporting some form of both.

        public static string[]? ShowBrowseDialog(Window? owner, string title, string? initialDirectory)
        {
            var dialog = new Ookii.Dialogs.Wpf.VistaOpenFileDialog(); // Use VistaOpenFileDialog
            dialog.Title = title;
            dialog.Multiselect = true; // Allow multiple selection
            dialog.InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            dialog.Filter = $"{LocalizationHelper.GetLocalizedString("AllFiles")}|*.*"; // Allow all file types by default in this dialog
            dialog.FilterIndex = 0; // Select the first filter

            // Configure the dialog to allow selecting folders as well
            // This requires modifying the dialog's options using reflection or a helper class,
            // or using a different dialog like FileSaveDialog from WindowsAPICodePack.
            // The simplest approach is to just allow file selection AND check if selected 'files' are folders.
            // VistaOpenFileDialog doesn't inherently return folder paths if you select folders in the UI.
            // This is tricky. Let's revert to the original code's implied behavior of showing a folder browser FIRST.

            // Revert to showing FolderBrowserDialog first, if cancelled, show OpenFileDialog.
            // This doesn't support selecting BOTH at once, but matches the spirit of 'files and/or folders'.
            string[]? selectedPaths = null;

            // Try Folder first
            string? folderPath = ShowFolderBrowserDialog(owner, LocalizationHelper.GetLocalizedString("SelectFolder"), initialDirectory);
            if (folderPath != null)
            {
                selectedPaths = new[] { folderPath };
            }
            else
            {
                // If folder selection cancelled, try files
                selectedPaths = ShowImageFileDialog(owner, LocalizationHelper.GetLocalizedString("SelectOneOrMoreImages"), initialDirectory, multiselect: true);
            }

            return selectedPaths; // Returns either folder(s) or file(s) or null
        }


        // Shows a dialog to select files AND/OR folders (async wrapper).
        // Implementation for IUIReporter.ShowBrowseDialogAsync.
        public static Task<string[]?> ShowBrowseDialogAsync(Window? owner, string title, string? initialDirectory, CancellationToken token)
        {
            // See comments in sync ShowBrowseDialog above. Implementing the simple folder-or-file fallback here.
            string[]? selectedPaths = null;

            // Show Folder first (blocking call on UI thread)
            string? folderPath = ShowFolderBrowserDialog(owner, LocalizationHelper.GetLocalizedString("SelectFolder"), initialDirectory);
            if (folderPath != null)
            {
                selectedPaths = new[] { folderPath };
            }
            else
            {
                // If folder selection cancelled, show files (blocking call on UI thread)
                selectedPaths = ShowImageFileDialog(owner, LocalizationHelper.GetLocalizedString("SelectOneOrMoreImages"), initialDirectory, multiselect: true);
            }

            return Task.FromResult(selectedPaths);
        }


        // Shows a dialog to save a file.
        // Returns selected file path or null if cancelled.
        public static string? ShowSaveFileDialog(Window? owner, string title, string? initialDirectory, string? defaultFileName, string filter, string defaultExtension)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultFileName,
                DefaultExt = defaultExtension,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);

            return result == true ? dialog.FileName : null;
        }

        // Shows a dialog to save a file (async wrapper).
        // Implementation for IUIReporter.ShowSaveFileDialogAsync.
        public static Task<string?> ShowSaveFileDialogAsync(Window? owner, string title, string? initialDirectory, string? defaultFileName, string filter, string defaultExtension, CancellationToken token)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                FileName = defaultFileName,
                DefaultExt = defaultExtension,
                InitialDirectory = initialDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            bool? result = owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
            return Task.FromResult(result == true ? dialog.FileName : null);
        }
    }
}