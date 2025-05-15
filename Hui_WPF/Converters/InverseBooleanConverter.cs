// Converters/InverseBooleanConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace Hui_WPF.Converters
{
    [ValueConversion(typeof(bool?), typeof(bool?))] // Indicate it can handle nullable
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return null; // If input is not bool, return null for bool? target
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            // If converting back to a non-nullable bool and value is null, this could be an issue.
            // However, IsChecked usually binds to bool? or bool. If source is bool, null from target is problematic.
            // For IsChecked (bool?) binding to GeneralEnableBackup (bool),
            // if IsChecked is null, what should GeneralEnableBackup become? Default to false.
            if (value == null && targetType == typeof(bool))
            {
                return false; // Or throw, or handle based on specific logic
            }
            return null;
        }
    }
}