// Hui_WPF/Views/DirectRenameView.xaml.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Hui_WPF.ViewModels;
using System.Windows.Input;
using System.Windows.Media;

namespace Hui_WPF.Views
{
    /// <summary>
    /// Interaction logic for DirectRenameView.xaml
    /// Configures options for directly renaming files or folders based on patterns.
    /// </summary>
    public partial class DirectRenameView : UserControl
    {
        private DirectRenameViewModel? ViewModel => DataContext as DirectRenameViewModel;

        // Ensure checkbox exclusivity for rename modes
        private void InitializeRenameModeExclusivity()
        {
            chkRenameFiles_RenameView.Checked += RenameModeCheckBox_Checked;
            chkRenameFolders_RenameView.Checked += RenameModeCheckBox_Checked;
            chkRenameBoth_RenameView.Checked += RenameModeCheckBox_Checked;
        }

        public DirectRenameView()
        {
            InitializeComponent();
            this.DataContextChanged += (s, e) => ViewModel?.UpdateNamingPreview();
            this.Loaded += (s, e) => ViewModel?.UpdateNamingPreview();
            // Set up mutual exclusion for rename mode checkboxes
            InitializeRenameModeExclusivity();
        }

        // Allow mouse wheel to scroll TreeView under nested ScrollViewer
        private void TreeView_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is TreeView tv)
            {
                // Find internal ScrollViewer
                var sv = FindVisualChild<ScrollViewer>(tv);
                if (sv != null)
                {
                    sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                    e.Handled = true;
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        // Handle mutual exclusion among rename mode checkboxes
        private void RenameModeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender == chkRenameFiles_RenameView)
            {
                chkRenameFolders_RenameView.IsChecked = false;
                chkRenameBoth_RenameView.IsChecked = false;
            }
            else if (sender == chkRenameFolders_RenameView)
            {
                chkRenameFiles_RenameView.IsChecked = false;
                chkRenameBoth_RenameView.IsChecked = false;
            }
            else if (sender == chkRenameBoth_RenameView)
            {
                chkRenameFiles_RenameView.IsChecked = false;
                chkRenameFolders_RenameView.IsChecked = false;
            }
        }
    }
}