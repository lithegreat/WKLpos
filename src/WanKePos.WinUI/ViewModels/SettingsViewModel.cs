using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Hardware;
using WanKePos.Infrastructure.Import;
using WanKePos.WinUI.Messages;
using WanKePos.WinUI.Services;

namespace WanKePos.WinUI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly ReceiptPrinter _receiptPrinter;
    private readonly IProductRepository _productRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly ExcelImporter _excelImporter;
    private readonly IUpdateService _updateService;
    private readonly IThemeService _themeService;

    [ObservableProperty]
    private StoreSettings _settings = new();

    [ObservableProperty]
    private int _selectedThemeIndex = 0; // 0: 跟随系统 (默认), 1: 浅色模式, 2: 黑暗模式

    [ObservableProperty]
    private string _appVersion = GetCurrentAppVersion();

    [ObservableProperty]
    private string _gitHubRepo = "lithegreat/WKLpos";

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private string _updateStatusText = "未检查";

    public ObservableCollection<string> AvailablePorts { get; } = new();

    public Func<Task<string?>>? RequestOpenFileDialog { get; set; }
    public Func<UpdateInfo, Task>? RequestUpdateDialog { get; set; }
    public Action<string, string>? ShowMessage { get; set; }

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        ReceiptPrinter receiptPrinter,
        IProductRepository productRepository,
        IMemberRepository memberRepository,
        ExcelImporter excelImporter,
        IUpdateService updateService,
        IThemeService themeService)
    {
        _settingsRepository = settingsRepository;
        _receiptPrinter = receiptPrinter;
        _productRepository = productRepository;
        _memberRepository = memberRepository;
        _excelImporter = excelImporter;
        _updateService = updateService;
        _themeService = themeService;

        _themeService.ThemeChanged += (s, themeName) =>
        {
            var idx = themeName switch
            {
                "Light" => 1,
                "Dark" => 2,
                _ => 0
            };
            if (SelectedThemeIndex != idx)
            {
                SelectedThemeIndex = idx;
            }
        };
    }

    private bool _isInitialized;

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        Settings = await _settingsRepository.GetSettingsAsync() ?? new StoreSettings();
        SelectedThemeIndex = Settings.AppTheme switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
        
        AvailablePorts.Clear();
        var ports = SerialPort.GetPortNames();
        foreach (var p in ports) AvailablePorts.Add(p);
    }

    partial void OnSelectedThemeIndexChanged(int value)
    {
        var theme = value switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "Default"
        };

        if (Settings.AppTheme != theme)
        {
            Settings.AppTheme = theme;
            _ = _themeService.SetThemeAsync(theme);
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        Settings.AppTheme = SelectedThemeIndex switch
        {
            1 => "Light",
            2 => "Dark",
            _ => "Default"
        };
        await _settingsRepository.SaveSettingsAsync(Settings);
        await _themeService.SetThemeAsync(Settings.AppTheme);
        ShowMessage?.Invoke("提示", "设置已成功保存！");
    }

    [RelayCommand]
    public async Task CheckUpdateAsync()
    {
        if (IsCheckingUpdate) return;
        IsCheckingUpdate = true;
        UpdateStatusText = "正在连接 GitHub 检查版本...";

        try
        {
            var updateInfo = await _updateService.CheckForUpdateAsync(GitHubRepo, AppVersion);
            if (updateInfo.HasUpdate)
            {
                UpdateStatusText = $"发现新版本: v{updateInfo.LatestVersion}";
                if (RequestUpdateDialog != null)
                {
                    await RequestUpdateDialog.Invoke(updateInfo);
                }
            }
            else
            {
                UpdateStatusText = $"已是最新版本 (v{AppVersion})";
                ShowMessage?.Invoke("检查更新", $"恭喜！当前运行的已是最新版本 (v{AppVersion})，暂无可用更新。");
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = "检查失败";
            ShowMessage?.Invoke("检查更新异常", $"无法连接到 GitHub Releases 服务：\n{ex.Message}\n请检查网络连接或 GitHub 仓库配置是否正确。");
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    public void TestPrint()
    {
        if (string.IsNullOrEmpty(Settings.PrinterPort))
        {
            ShowMessage?.Invoke("提示", "请先选择打印机端口");
            return;
        }
        try
        {
            _receiptPrinter.PortName = Settings.PrinterPort;
            _receiptPrinter.BaudRate = Settings.PrinterBaudRate;
            _receiptPrinter.Connect();
            _receiptPrinter.TestPrint();
            _receiptPrinter.Disconnect();
            ShowMessage?.Invoke("提示", "测试打印指令已发送！");
        }
        catch (Exception ex)
        {
            ShowMessage?.Invoke("错误", $"打印机错误: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ImportProductsAsync()
    {
        if (RequestOpenFileDialog != null)
        {
            var filePath = await RequestOpenFileDialog.Invoke();
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    var products = await _excelImporter.ImportProductsAsync(filePath);
                    await _productRepository.ImportFromListAsync(products);
                    ShowMessage?.Invoke("导入成功", $"成功导入 {products.Count} 个商品。");
                    WeakReferenceMessenger.Default.Send(new ProductsChangedMessage());
                }
                catch (Exception ex)
                {
                    ShowMessage?.Invoke("错误", $"导入失败: {ex.Message}");
                }
            }
        }
    }

    [RelayCommand]
    public async Task ImportMembersAsync()
    {
        if (RequestOpenFileDialog != null)
        {
            var filePath = await RequestOpenFileDialog.Invoke();
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    var members = await _excelImporter.ImportMembersAsync(filePath);
                    await _memberRepository.ImportFromListAsync(members);
                    ShowMessage?.Invoke("导入成功", $"成功导入 {members.Count} 个会员。");
                }
                catch (Exception ex)
                {
                    ShowMessage?.Invoke("错误", $"导入失败: {ex.Message}");
                }
            }
        }
    }

    private static string GetCurrentAppVersion()
    {
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (ver == null || (ver.Major == 0 && ver.Minor == 0 && ver.Build == 0))
        {
            return "1.1.0";
        }
        return ver.Build >= 0 ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : $"{ver.Major}.{ver.Minor}.0";
    }
}
