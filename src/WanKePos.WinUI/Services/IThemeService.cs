using Microsoft.UI.Xaml;
using System;
using System.Threading.Tasks;

namespace WanKePos.WinUI.Services;

/// <summary>
/// 客户端界面主题服务接口 (支持跟随系统、浅色模式与黑暗模式)
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// 当前配置的主题名称: "Default" (跟随系统), "Light" (浅色), "Dark" (深色)
    /// </summary>
    string CurrentTheme { get; }

    /// <summary>
    /// 当前对应的 WinUI 3 ElementTheme 枚举
    /// </summary>
    ElementTheme CurrentElementTheme { get; }

    /// <summary>
    /// 主题变更事件
    /// </summary>
    event EventHandler<string>? ThemeChanged;

    /// <summary>
    /// 初始化主题配置并绑定到主窗口
    /// </summary>
    Task InitializeAsync(Window window);

    /// <summary>
    /// 切换并持久化主题配置
    /// </summary>
    Task SetThemeAsync(string themeName);

    /// <summary>
    /// 同步 Windows 原生窗口标题栏的深浅色外观
    /// </summary>
    void UpdateTitleBarTheme(ElementTheme actualTheme);
}
