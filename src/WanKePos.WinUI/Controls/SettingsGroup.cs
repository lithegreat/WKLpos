using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WanKePos.WinUI.Controls;

/// <summary>
/// 参考微软 PowerToys 设计的设置卡片分组容器
/// </summary>
public class SettingsGroup : ItemsControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsGroup), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsGroup), new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty DescriptionVisibilityProperty =
        DependencyProperty.Register(nameof(DescriptionVisibility), typeof(Visibility), typeof(SettingsGroup), new PropertyMetadata(Visibility.Collapsed));

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public Visibility DescriptionVisibility
    {
        get => (Visibility)GetValue(DescriptionVisibilityProperty);
        set => SetValue(DescriptionVisibilityProperty, value);
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsGroup group)
        {
            group.DescriptionVisibility = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public SettingsGroup()
    {
        this.DefaultStyleKey = typeof(SettingsGroup);
    }
}
