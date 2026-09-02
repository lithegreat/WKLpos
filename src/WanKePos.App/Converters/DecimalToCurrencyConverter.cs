using System;
using System.Windows.Data;
using System.Globalization;

namespace WanKePos.App.Converters
{
    public class DecimalToCurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
            {
                return $"¥{d:##0.00}";
            }
            return "¥0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
