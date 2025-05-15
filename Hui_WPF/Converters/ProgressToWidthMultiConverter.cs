using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Hui_WPF.Converters
{
    public class ProgressToWidthMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 &&
                values[0] is double progressValue &&
                values[1] is double targetWidth &&
                targetWidth > 0)
            {
                double clampedProgress = Math.Max(0.0, Math.Min(100.0, progressValue));
                return (clampedProgress / 100.0) * targetWidth;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}