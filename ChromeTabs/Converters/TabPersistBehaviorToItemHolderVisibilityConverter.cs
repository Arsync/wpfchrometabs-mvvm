﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ChromeTabs.Converters
{
    public class TabPersistBehaviorToItemHolderVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case TabPersistMode.All:
                case TabPersistMode.Timed:
                    return Visibility.Visible;

                default:
                    return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
