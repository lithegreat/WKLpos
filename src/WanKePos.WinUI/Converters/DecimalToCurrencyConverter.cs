using Microsoft.UI.Xaml.Data;
using System;

namespace WanKePos.WinUI.Converters
{
    public class DecimalToCurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is decimal decimalValue)
            {
                return $"¥{decimalValue:N2}";
            }
            if (value is double doubleValue)
            {
                return $"¥{doubleValue:N2}";
            }
            return "¥0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
