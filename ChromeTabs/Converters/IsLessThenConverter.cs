using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChromeTabs.Converters
{
    public class IsLessThanConverter : DependencyObject, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var param = System.Convert.ToDouble(parameter);
            var width = System.Convert.ToDouble(value);

            return width > 0 && width < param;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
