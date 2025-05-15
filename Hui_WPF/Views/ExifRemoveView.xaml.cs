// Hui_WPF/Views/ExifRemoveView.xaml.cs
using System.Windows;
using System.Windows.Controls;

namespace Hui_WPF.Views
{
    /// <summary>
    /// Interaction logic for ExifRemoveView.xaml
    /// Allows users to specify which EXIF/metadata tags to remove.
    /// </summary>
    public partial class ExifRemoveView : UserControl
    {
        public ExifRemoveView()
        {
            InitializeComponent();
            // Set initial state based on default CheckBox values
            UpdateKeepOptionsState();
        }

        // --- Public Properties for MainWindow ---
        public bool RemoveAllMetadata => chkRemoveAllExif_RemoveView?.IsChecked ?? true;
        public bool KeepDateTaken => chkKeepDateTaken_RemoveView?.IsChecked ?? false;
        public bool KeepGps => chkKeepGps_RemoveView?.IsChecked ?? false;
        public bool KeepOrientation => chkKeepOrientation_RemoveView?.IsChecked ?? false;
        public bool KeepCameraInfo => chkKeepCameraInfo_RemoveView?.IsChecked ?? false;
        public bool KeepColorSpace => chkKeepColorSpace_RemoveView?.IsChecked ?? false;
        public bool RemoveThumbnail => chkRemoveThumbnail_RemoveView?.IsChecked ?? true;

        // --- Event Handlers ---
        private void ChkRemoveAll_Changed(object sender, RoutedEventArgs e)
        {
            UpdateKeepOptionsState();
        }

        // --- Internal UI Logic ---
        private void UpdateKeepOptionsState()
        {
            if (grpKeepSpecific_RemoveView != null && chkRemoveAllExif_RemoveView != null)
            {
                // Enable the "Keep Specific" group only if "Remove All" is UNCHECKED
                grpKeepSpecific_RemoveView.IsEnabled = !(chkRemoveAllExif_RemoveView.IsChecked ?? true);
            }
        }
    }
}