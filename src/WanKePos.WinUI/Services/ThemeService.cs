using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WanKePos.Domain.Interfaces;
using WinRT.Interop;

namespace WanKePos.WinUI.Services;

/// <summary>
/// 客户端界面主题服务实现
/// 负责在运行时无刷新切换主题、同步 Windows 沉浸式暗色标题栏并持久化用户偏好
/// </summary>
public class ThemeService : IThemeService
{
    private readonly ISettingsRepository _settingsRepository;
    private Window? _window;
    private IntPtr _hwnd = IntPtr.Zero;

    public string CurrentTheme { get; private set; } = "Default";

    public ElementTheme CurrentElementTheme => MapToElementTheme(CurrentTheme);

    public event EventHandler<string>? ThemeChanged;

    public ThemeService(ISettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public async Task InitializeAsync(Window window)
    {
        _window = window;
        _hwnd = WindowNative.GetWindowHandle(window);

        // 从持久化存储读取用户主题设置 (默认跟随系统 "Default")
        var settings = await _settingsRepository.GetSettingsAsync();
        CurrentTheme = string.IsNullOrWhiteSpace(settings?.AppTheme) ? "Default" : settings.AppTheme;

        ApplyThemeToRootElement();

        if (_window.Content is FrameworkElement rootElement)
        {
            // 监听系统深浅色切换事件 (仅在跟随系统模式下自适应触发，或用于更新原生标题栏)
            rootElement.ActualThemeChanged += (s, e) =>
            {
                UpdateTitleBarTheme(rootElement.ActualTheme);
            };

            UpdateTitleBarTheme(rootElement.ActualTheme);
        }
    }

    public async Task SetThemeAsync(string themeName)
    {
        if (themeName != "Light" && themeName != "Dark")
        {
            themeName = "Default";
        }

        CurrentTheme = themeName;
        ApplyThemeToRootElement();

        if (_window?.Content is FrameworkElement rootElement)
        {
            UpdateTitleBarTheme(rootElement.ActualTheme);
        }

        // 保存到数据库
        try
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            if (settings != null && settings.AppTheme != themeName)
            {
                settings.AppTheme = themeName;
                await _settingsRepository.SaveSettingsAsync(settings);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ThemeService] 保存主题异常: {ex.Message}");
        }

        ThemeChanged?.Invoke(this, CurrentTheme);
    }

    private void ApplyThemeToRootElement()
    {
        if (_window?.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = MapToElementTheme(CurrentTheme);
        }
    }

    public void UpdateTitleBarTheme(ElementTheme actualTheme)
    {
        if (_hwnd == IntPtr.Zero) return;

        // 1: 暗色标题栏, 0: 浅色标题栏
        int isDark = (actualTheme == ElementTheme.Dark) ? 1 : 0;

        // Windows 10 (20H1+) 及 Windows 11: DWMWA_USE_IMMERSIVE_DARK_MODE = 20
        int hr = DwmSetWindowAttribute(_hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref isDark, sizeof(int));
        if (hr != 0)
        {
            // Windows 10 (1809~1903): DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19
            DwmSetWindowAttribute(_hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref isDark, sizeof(int));
        }
    }

    public static ElementTheme MapToElementTheme(string themeName) => themeName switch
    {
        "Light" => ElementTheme.Light,
        "Dark" => ElementTheme.Dark,
        _ => ElementTheme.Default
    };

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
}
