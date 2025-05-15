using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Hui_WPF.Converters
{
    [ValueConversion(typeof(double), typeof(Brush))]
    public class ProgressToBrushConverter : IValueConverter
    {
        public Brush LowBrush { get; set; } = new SolidColorBrush(Color.FromArgb(255, 0xDC, 0x35, 0x45));
        public Brush MediumBrush { get; set; } = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xC1, 0x07));
        public Brush HighBrush { get; set; } = new SolidColorBrush(Color.FromArgb(255, 0x17, 0xA2, 0xB8));
        public Brush CompleteBrush { get; set; } = new SolidColorBrush(Color.FromArgb(255, 0x28, 0xA7, 0x45));

        public double CompleteThreshold { get; set; } = 100.0;
        public double HighThreshold { get; set; } = 75.0;
        public double MediumThreshold { get; set; } = 35.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double progressValue;

            try
            {
                progressValue = System.Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return Binding.DoNothing;
            }

            if (progressValue >= CompleteThreshold)
            {
                return CompleteBrush;
            }
            else if (progressValue >= HighThreshold)
            {
                return HighBrush;
            }
            else if (progressValue >= MediumThreshold)
            {
                return MediumBrush;
            }
            else
            {
                return LowBrush;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"{nameof(ProgressToBrushConverter)} cannot convert back from Brush to double.");
        }
    }
}