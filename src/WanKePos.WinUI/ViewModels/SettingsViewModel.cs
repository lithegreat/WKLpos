using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Reflection;
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

    [ObservableProperty]
    private bool _hasUpdateAvailable = false;

    [ObservableProperty]
    private UpdateInfo? _latestUpdateInfo;

    [ObservableProperty]
    private string _updateInfoBarTitle = string.Empty;

    [ObservableProperty]
    private string _updateInfoBarMessage = string.Empty;

    [ObservableProperty]
    private string _selectedCategoryTag = "Store";

    [ObservableProperty]
    private string _autoSaveStatusText = "所有设置已自动保存";

    [ObservableProperty]
    private bool _isAutoSaving = false;

    private bool _isLoadingSettings = false;
    private CancellationTokenSource? _autoSaveCts;
    private readonly System.Threading.SemaphoreSlim _saveLock = new(1, 1);

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

        Settings.PropertyChanged += OnSettingsEntityPropertyChanged;

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

        // 订阅后台启动检查检测到的更新消息
        WeakReferenceMessenger.Default.Register<UpdateAvailableMessage>(this, (r, m) =>
        {
            HasUpdateAvailable = true;
            LatestUpdateInfo = m.UpdateInfo;
            UpdateInfoBarTitle = $"发现新版本: v{m.UpdateInfo.LatestVersion} ({m.UpdateInfo.ChannelText})";
            UpdateInfoBarMessage = $"发布于 {m.UpdateInfo.PublishedAt:yyyy-MM-dd}。包含功能优化与最新修复，点击【立即更新】快速升级。";
            UpdateStatusText = $"发现新版本: v{m.UpdateInfo.LatestVersion} ({m.UpdateInfo.ChannelText})";
        });
    }

    partial void OnSettingsChanged(StoreSettings? oldValue, StoreSettings newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnSettingsEntityPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnSettingsEntityPropertyChanged;
        }
    }

    private void OnSettingsEntityPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isLoadingSettings) return;

        bool immediate = e?.PropertyName switch
        {
            nameof(StoreSettings.EnablePreviewUpdates) => true,
            nameof(StoreSettings.AutoCheckUpdatesOnStartup) => true,
            nameof(StoreSettings.PrinterPort) => true,
            nameof(StoreSettings.AppTheme) => true,
            _ => false
        };

        ScheduleAutoSave(immediate);
    }

    public void ScheduleAutoSave(bool immediate = false)
    {
        if (_isLoadingSettings) return;

        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                if (!immediate)
                {
                    await Task.Delay(400, token);
                }
                if (token.IsCancellationRequested) return;

                await SaveSettingsInternalAsync();
            }
            catch (OperationCanceledException)
            {
                // 防抖忽略
            }
            catch (Exception)
            {
                AutoSaveStatusText = "自动保存失败";
            }
        });
    }

    public async Task FlushAutoSaveAsync()
    {
        if (_isLoadingSettings) return;
        _autoSaveCts?.Cancel();
        await SaveSettingsInternalAsync();
    }

    private async Task SaveSettingsInternalAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            IsAutoSaving = true;
            AutoSaveStatusText = "正在保存设置...";

            Settings.AppTheme = SelectedThemeIndex switch
            {
                1 => "Light",
                2 => "Dark",
                _ => "Default"
            };

            await _settingsRepository.SaveSettingsAsync(Settings);
            await _themeService.SetThemeAsync(Settings.AppTheme);

            // 发送设置更新消息通知主窗口 (如实时同步门店名称)
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage(Settings));

            AutoSaveStatusText = "所有设置已自动保存";
        }
        catch (Exception)
        {
            AutoSaveStatusText = "自动保存失败";
        }
        finally
        {
            IsAutoSaving = false;
            _saveLock.Release();
        }
    }

    private bool _isInitialized;

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        _isLoadingSettings = true;
        try
        {
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

            AutoSaveStatusText = "所有设置已自动保存";
        }
        finally
        {
            _isLoadingSettings = false;
        }
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
            if (!_isLoadingSettings)
            {
                ScheduleAutoSave(immediate: true);
            }
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        await FlushAutoSaveAsync();
    }

    [RelayCommand]
    public async Task ShowUpdateDialogAsync()
    {
        if (LatestUpdateInfo != null && RequestUpdateDialog != null)
        {
            await RequestUpdateDialog.Invoke(LatestUpdateInfo);
        }
        else
        {
            await CheckUpdateAsync();
        }
    }

    [RelayCommand]
    public async Task CheckUpdateAsync()
    {
        if (IsCheckingUpdate) return;
        IsCheckingUpdate = true;
        var channelText = Settings.EnablePreviewUpdates ? "包含预览版渠道" : "正式版渠道";
        UpdateStatusText = $"正在连接 GitHub 检查版本 ({channelText})...";

        try
        {
            var updateInfo = await _updateService.CheckForUpdateAsync(GitHubRepo, AppVersion, Settings.EnablePreviewUpdates);
            if (updateInfo.HasUpdate)
            {
                HasUpdateAvailable = true;
                LatestUpdateInfo = updateInfo;
                UpdateInfoBarTitle = $"发现新版本: v{updateInfo.LatestVersion} ({updateInfo.ChannelText})";
                UpdateInfoBarMessage = $"发布于 {updateInfo.PublishedAt:yyyy-MM-dd}。包含功能优化与最新修复，点击【立即更新】快速升级。";
                UpdateStatusText = $"发现新版本: v{updateInfo.LatestVersion} ({updateInfo.ChannelText})";
                WeakReferenceMessenger.Default.Send(new UpdateAvailableMessage(updateInfo));
                if (RequestUpdateDialog != null)
                {
                    await RequestUpdateDialog.Invoke(updateInfo);
                }
            }
            else
            {
                HasUpdateAvailable = false;
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
        try
        {
            var infoVer = System.Reflection.Assembly.GetExecutingAssembly()
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(infoVer))
            {
                var clean = infoVer.Split('+')[0].Trim().TrimStart('v', 'V');
                if (!string.IsNullOrWhiteSpace(clean))
                {
                    return clean;
                }
            }

            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            if (ver != null && !(ver.Major == 0 && ver.Minor == 0 && ver.Build == 0))
            {
                return ver.Build >= 0 ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : $"{ver.Major}.{ver.Minor}.0";
            }
        }
        catch { }
        return "0.2.3";
    }
}
