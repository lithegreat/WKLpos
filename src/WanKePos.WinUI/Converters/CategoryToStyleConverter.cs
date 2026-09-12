using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using WanKePos.WinUI.ViewModels;

namespace WanKePos.WinUI.Converters;

/// <summary>
/// 品类按钮高亮转换器：
/// 对比品类文本与当前选中的品类，匹配时返回 AccentButtonStyle
/// </summary>
public class CategoryToStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string category)
        {
            var vm = App.Services?.GetService(typeof(PurchaseOrderViewModel)) as PurchaseOrderViewModel;
            var current = vm?.SelectedCategory;
            if (!string.IsNullOrEmpty(current) && string.Equals(category, current, StringComparison.OrdinalIgnoreCase))
            {
                if (Application.Current.Resources.TryGetValue("CategoryItemSelectedStyle", out var accentStyle))
                {
                    return accentStyle;
                }
            }
        }

        if (Application.Current.Resources.TryGetValue("CategoryItemButtonStyle", out var defaultStyle))
        {
            return defaultStyle;
        }

        if (Application.Current.Resources.TryGetValue("DefaultButtonStyle", out var fallbackStyle))
        {
            return fallbackStyle;
        }

        return DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
