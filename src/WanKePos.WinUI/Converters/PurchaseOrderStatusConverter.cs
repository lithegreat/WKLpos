using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using WanKePos.Domain.Enums;

namespace WanKePos.WinUI.Converters;

/// <summary>
/// 采购单状态枚举多功能转换器：
/// - 无参数：枚举 → 中文显示文本
/// - ConverterParameter="Background"：枚举 → 背景色 Brush
/// - ConverterParameter="Foreground"：枚举 → 前景色 Brush
/// - ConverterParameter="DraftOnly"：Draft → Visible, 其它 → Collapsed
/// - ConverterParameter="NotCancelled"：Cancelled → Collapsed, 其它 → Visible
/// - ConverterParameter="NotReceived"：Received → Collapsed, 其它 → Visible
/// </summary>
public class PurchaseOrderStatusConverter : IValueConverter
{
    // 预缓存 Brush 对象避免重复创建
    private static readonly SolidColorBrush DraftBackground = new(ColorHelper.FromArgb(255, 218, 235, 255));    // 浅蓝
    private static readonly SolidColorBrush ReceivedBackground = new(ColorHelper.FromArgb(255, 212, 237, 218)); // 浅绿
    private static readonly SolidColorBrush CancelledBackground = new(ColorHelper.FromArgb(255, 230, 230, 230));// 浅灰

    private static readonly SolidColorBrush DraftForeground = new(ColorHelper.FromArgb(255, 0, 90, 180));       // 蓝
    private static readonly SolidColorBrush ReceivedForeground = new(ColorHelper.FromArgb(255, 30, 130, 50));   // 绿
    private static readonly SolidColorBrush CancelledForeground = new(ColorHelper.FromArgb(255, 120, 120, 120));// 灰

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not PurchaseOrderStatus status)
            return string.Empty;

        var param = parameter as string;

        return param switch
        {
            "Background" => status switch
            {
                PurchaseOrderStatus.Draft => DraftBackground,
                PurchaseOrderStatus.Received => ReceivedBackground,
                PurchaseOrderStatus.Cancelled => CancelledBackground,
                _ => DraftBackground
            },
            "Foreground" => status switch
            {
                PurchaseOrderStatus.Draft => DraftForeground,
                PurchaseOrderStatus.Received => ReceivedForeground,
                PurchaseOrderStatus.Cancelled => CancelledForeground,
                _ => DraftForeground
            },
            "DraftOnly" => status == PurchaseOrderStatus.Draft
                ? Visibility.Visible : Visibility.Collapsed,
            "NotCancelled" => status != PurchaseOrderStatus.Cancelled
                ? Visibility.Visible : Visibility.Collapsed,
            "NotReceived" => status != PurchaseOrderStatus.Received
                ? Visibility.Visible : Visibility.Collapsed,
            _ => status switch
            {
                PurchaseOrderStatus.Draft => "待入库",
                PurchaseOrderStatus.Received => "已入库",
                PurchaseOrderStatus.Cancelled => "已取消",
                _ => status.ToString()
            }
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
