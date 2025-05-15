// Hui_WPF/Views/GenerateBurstView.xaml.cs
using System.Windows.Controls;
using Hui_WPF.ViewModels; // Use ViewModel namespace

namespace Hui_WPF.Views
{
    // Code-behind for GenerateBurstView.xaml.
    // Should contain minimal UI-specific logic, mostly InitializeComponent().
    // Interactions and state are handled by GenerateBurstViewModel.
    public partial class GenerateBurstView : UserControl
    {
        // ViewModel instance (can be accessed via DataContext)
        private GenerateBurstViewModel? ViewModel => DataContext as GenerateBurstViewModel;

        public GenerateBurstView()
        {
            InitializeComponent();

            // SelectionChanged handler for ComboBox is not needed if binding SelectedValue
            // cmbBurstFps_BurstView.SelectionChanged += CmbBurstFps_SelectionChanged;

            // Checked/Unchecked handlers for RadioButtons are not needed if binding IsChecked with converter
            // rbFormatMov_BurstView.Checked += RadioButton_Format_Checked;
            // ...
        }

        // Removed event handlers and properties from code-behind.
        // Properties like OutputFileNameBase, Framerate, BurstOutputFormat are now in ViewModel.
        // The XAML binds directly to these ViewModel properties.
    }
}