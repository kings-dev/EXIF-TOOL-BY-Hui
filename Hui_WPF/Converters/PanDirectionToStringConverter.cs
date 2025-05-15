using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Hui_WPF.Models;
using Hui_WPF.Utils;

namespace Hui_WPF.Converters
{
    public class PanDirectionToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PanDirection direction)
            {
                switch (direction)
                {
                    case PanDirection.None: return LocalizationHelper.GetLocalizedString("PanDirection_None", "无");
                    case PanDirection.Up: return LocalizationHelper.GetLocalizedString("PanDirection_Up", "上");
                    case PanDirection.Down: return LocalizationHelper.GetLocalizedString("PanDirection_Down", "下");
                    case PanDirection.Left: return LocalizationHelper.GetLocalizedString("PanDirection_Left", "左");
                    case PanDirection.Right: return LocalizationHelper.GetLocalizedString("PanDirection_Right", "右");
                    default: return direction.ToString();
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert localized string back to PanDirection.");
        }
    }
}