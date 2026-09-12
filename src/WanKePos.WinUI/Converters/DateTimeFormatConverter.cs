using Microsoft.UI.Xaml.Data;
using System;

namespace WanKePos.WinUI.Converters;

/// <summary>
/// DateTime → 友好紧凑格式 (MM-dd HH:mm) 转换器
/// </summary>
public class DateTimeFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dt)
        {
            // 同年只显示 MM-dd HH:mm，跨年显示完整年份
            return dt.Year == DateTime.Now.Year
                ? dt.ToString("MM-dd HH:mm")
                : dt.ToString("yyyy-MM-dd HH:mm");
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
