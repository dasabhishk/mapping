using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CMMT.Helpers {  
    public class InvertBooleanConverter : IValueConverter
    {
        // Converts bool to Visibility (inverted): true -> Collapsed, false -> Visible
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType == typeof(Visibility))
            {
                if (value is bool boolValue)
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                return Visibility.Visible;
            }
            // Fallback: just invert boolean
            if (value is bool b)
                return !b;
            return value;
        }

        // Converts back from Visibility or bool (inverted)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
                return visibility != Visibility.Visible;
            if (value is bool b)
                return !b;
            return value;
        }
    }
}