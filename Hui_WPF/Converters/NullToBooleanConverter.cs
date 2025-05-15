using System;
using System.Globalization;
using System.Windows.Data;

namespace Hui_WPF.Converters
{
    public class NullToBooleanConverter : IValueConverter
    {
        public bool TrueIfNotNull { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool behaviorTrueIfNotNull = this.TrueIfNotNull;
            if (parameter is string paramString && bool.TryParse(paramString, out bool parsedParam))
            {
                behaviorTrueIfNotNull = parsedParam;
            }
            else if (parameter is bool pBool)
            {
                behaviorTrueIfNotNull = pBool;
            }

            if (behaviorTrueIfNotNull)
            {
                return value != null;
            }
            else
            {
                return value == null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("NullToBooleanConverter is a OneWay converter.");
        }
    }
}