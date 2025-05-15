using System;
using System.Globalization;
using System.Windows.Data;

namespace Hui_WPF.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;

            string enumValueString = value.ToString()!;
            string parameterString = parameter.ToString()!;
            //string parameterString = parameter.ToString() ?? ""; // Handle null for parameterString

            if (value.GetType() == parameter.GetType())
            {
                return value.Equals(parameter);
            }

            if (enumValueString.Equals(parameterString, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                object parameterAsEnum = Enum.Parse(value.GetType(), parameterString, true);
                return value.Equals(parameterAsEnum);
            }
            catch
            {
                return false;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is true && parameter != null)
            {
                string? parameterString = parameter.ToString();
                if (parameterString != null)
                {
                    try
                    {
                        return Enum.Parse(targetType, parameterString, true);
                    }
                    catch
                    {
                        if (parameter.GetType() == targetType)
                        {
                            return parameter;
                        }
                    }
                }
            }
            return Binding.DoNothing;
        }
    }
}