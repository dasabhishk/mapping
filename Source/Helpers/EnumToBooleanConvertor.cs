using System;
using System.Globalization;
using System.Windows.Data;

namespace CMMT.Helpers 
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string enumValue = value.ToString();
            string parameterValue = parameter.ToString();

            return enumValue.Equals(parameterValue, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Binding.DoNothing;

            bool isChecked = (bool)value;
            if (!isChecked)
                return Binding.DoNothing;

            return Enum.Parse(targetType, parameter.ToString());
        }
    }
}