// Hui_WPF/Views/ExifWriteView.xaml.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Hui_WPF.Views
{
    /// <summary>
    /// Interaction logic for ExifWriteView.xaml
    /// Allows users to specify metadata values to write into image files.
    /// </summary>
    public partial class ExifWriteView : UserControl
    {
        public ExifWriteView()
        {
            InitializeComponent();
            // Set initial state based on default CheckBox values
            UpdateGroupBoxEnableStates();
            // Set DatePicker to today's date initially if not set by binding
            dpDateTaken_WriteView.SelectedDate ??= DateTime.Today;
        }

        // --- Public Properties for MainWindow ---

        // Common Tags
        public bool WriteCommonTags => chkWriteCommonTags_WriteView?.IsChecked ?? false;
        public string? Artist => GetTextBoxValue(txtArtist_WriteView);
        public string? Copyright => GetTextBoxValue(txtCopyright_WriteView);
        public string? Comment => GetTextBoxValue(txtComment_WriteView);
        public string? Description => GetTextBoxValue(txtDescription_WriteView);
        public int? Rating // Return null if invalid or empty
        {
            get
            {
                if (int.TryParse(txtRating_WriteView?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rating) && rating >= 0 && rating <= 5)
                {
                    return rating;
                }
                return null;
            }
        }


        // Date/Time Taken
        public bool WriteDateTaken => chkWriteDateTaken_WriteView?.IsChecked ?? false;
        public DateTime? DateTimeOriginal // Returns combined DateTime or null if invalid
        {
            get
            {
                if (!(dpDateTaken_WriteView?.SelectedDate is DateTime datePart)) return null;

                if (!int.TryParse(txtHourTaken_WriteView?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hour) || hour < 0 || hour > 23) return null;
                if (!int.TryParse(txtMinuteTaken_WriteView?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minute) || minute < 0 || minute > 59) return null;
                if (!int.TryParse(txtSecondTaken_WriteView?.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int second) || second < 0 || second > 59) return null;

                try
                {
                    return new DateTime(datePart.Year, datePart.Month, datePart.Day, hour, minute, second);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null; // Date/time combination was invalid
                }
            }
        }

        // GPS
        public bool WriteGps => chkWriteGps_WriteView?.IsChecked ?? false;
        public double? Latitude // Returns null if invalid or empty
        {
            get
            {
                if (double.TryParse(txtLatitude_WriteView?.Text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double lat) && lat >= -90.0 && lat <= 90.0)
                {
                    return lat;
                }
                return null;
            }
        }
        public double? Longitude // Returns null if invalid or empty
        {
            get
            {
                if (double.TryParse(txtLongitude_WriteView?.Text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double lon) && lon >= -180.0 && lon <= 180.0)
                {
                    return lon;
                }
                return null;
            }
        }


        // Helper to get trimmed textbox value or null if empty/whitespace
        private string? GetTextBoxValue(TextBox? textBox)
        {
            string? text = textBox?.Text?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }


        // --- Event Handlers ---
        private void ChkWriteEnable_Changed(object sender, RoutedEventArgs e)
        {
            UpdateGroupBoxEnableStates();
        }

        // --- Internal UI Logic ---
        private void UpdateGroupBoxEnableStates()
        {
            if (!this.IsLoaded) return; // Ensure controls are ready

            if (grpWriteCommonTags_WriteView != null && chkWriteCommonTags_WriteView != null)
            {
                grpWriteCommonTags_WriteView.IsEnabled = chkWriteCommonTags_WriteView.IsChecked ?? false;
            }
            if (grpWriteDateTaken_WriteView != null && chkWriteDateTaken_WriteView != null)
            {
                grpWriteDateTaken_WriteView.IsEnabled = chkWriteDateTaken_WriteView.IsChecked ?? false;
            }
            if (grpWriteGps_WriteView != null && chkWriteGps_WriteView != null)
            {
                grpWriteGps_WriteView.IsEnabled = chkWriteGps_WriteView.IsChecked ?? false;
            }
        }
    }
}