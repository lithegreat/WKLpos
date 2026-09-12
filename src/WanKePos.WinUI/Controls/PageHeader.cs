using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WanKePos.WinUI.Controls;

/// <summary>
/// 参考微软 PowerToys 设计的标准页面大标题与描述组件
/// </summary>
public class PageHeader : ContentControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, OnDescriptionChanged));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, OnGlyphChanged));

    public static readonly DependencyProperty BadgeTextProperty =
        DependencyProperty.Register(nameof(BadgeText), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty, OnBadgeTextChanged));

    public static readonly DependencyProperty DescriptionVisibilityProperty =
        DependencyProperty.Register(nameof(DescriptionVisibility), typeof(Visibility), typeof(PageHeader), new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty GlyphVisibilityProperty =
        DependencyProperty.Register(nameof(GlyphVisibility), typeof(Visibility), typeof(PageHeader), new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty BadgeVisibilityProperty =
        DependencyProperty.Register(nameof(BadgeVisibility), typeof(Visibility), typeof(PageHeader), new PropertyMetadata(Visibility.Collapsed));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
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

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public Visibility DescriptionVisibility
    {
        get => (Visibility)GetValue(DescriptionVisibilityProperty);
        set => SetValue(DescriptionVisibilityProperty, value);
    }

    public Visibility GlyphVisibility
    {
        get => (Visibility)GetValue(GlyphVisibilityProperty);
        set => SetValue(GlyphVisibilityProperty, value);
    }

    public Visibility BadgeVisibility
    {
        get => (Visibility)GetValue(BadgeVisibilityProperty);
        set => SetValue(BadgeVisibilityProperty, value);
    }

    private static void OnDescriptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageHeader header)
        {
            header.DescriptionVisibility = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static void OnGlyphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageHeader header)
        {
            header.GlyphVisibility = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static void OnBadgeTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PageHeader header)
        {
            header.BadgeVisibility = string.IsNullOrWhiteSpace(e.NewValue as string) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public PageHeader()
    {
        this.DefaultStyleKey = typeof(PageHeader);
    }
}
