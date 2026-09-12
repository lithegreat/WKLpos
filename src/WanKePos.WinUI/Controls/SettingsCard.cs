using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WanKePos.WinUI.Controls;

/// <summary>
/// 参考微软 PowerToys SettingsCard 设计的卡片设置控件
/// </summary>
public class SettingsCard : ContentControl
{
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty, OnGlyphChanged));

    public static readonly DependencyProperty HeaderIconVisibilityProperty =
        DependencyProperty.Register(nameof(HeaderIconVisibility), typeof(Visibility), typeof(SettingsCard), new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty DescriptionVisibilityProperty =
        DependencyProperty.Register(nameof(DescriptionVisibility), typeof(Visibility), typeof(SettingsCard), new PropertyMetadata(Visibility.Collapsed));

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

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public Visibility HeaderIconVisibility
    {
        get => (Visibility)GetValue(HeaderIconVisibilityProperty);
        set => SetValue(HeaderIconVisibilityProperty, value);
    }

    public Visibility DescriptionVisibility
    {
        get => (Visibility)GetValue(DescriptionVisibilityProperty);
        set => SetValue(DescriptionVisibilityProperty, value);
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsCard card)
        {
            card.DescriptionVisibility = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsCard card)
        {
            card.HeaderIconVisibility = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public SettingsCard()
    {
        this.DefaultStyleKey = typeof(SettingsCard);
    }
}
