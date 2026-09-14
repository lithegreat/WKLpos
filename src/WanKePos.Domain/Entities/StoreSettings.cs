using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 门店设置实体
/// </summary>
public class StoreSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private int _id;
    public int Id
    {
        get => _id;
        set { if (_id != value) { _id = value; OnPropertyChanged(); } }
    }
    
    private string _storeName = string.Empty;
    /// <summary>
    /// 门店名称
    /// </summary>
    public string StoreName
    {
        get => _storeName;
        set { if (_storeName != value) { _storeName = value; OnPropertyChanged(); } }
    }
    
    private string? _storeAddress;
    /// <summary>
    /// 门店地址
    /// </summary>
    public string? StoreAddress
    {
        get => _storeAddress;
        set { if (_storeAddress != value) { _storeAddress = value; OnPropertyChanged(); } }
    }
    
    private string? _storePhone;
    /// <summary>
    /// 门店电话
    /// </summary>
    public string? StorePhone
    {
        get => _storePhone;
        set { if (_storePhone != value) { _storePhone = value; OnPropertyChanged(); } }
    }
    
    private decimal _pointsPerYuan = 1m;
    /// <summary>
    /// 每消费N元获1积分 (default 1)
    /// </summary>
    public decimal PointsPerYuan
    {
        get => _pointsPerYuan;
        set { if (_pointsPerYuan != value) { _pointsPerYuan = value; OnPropertyChanged(); } }
    }
    
    private string? _printerPort;
    /// <summary>
    /// 打印机端口
    /// </summary>
    public string? PrinterPort
    {
        get => _printerPort;
        set { if (_printerPort != value) { _printerPort = value; OnPropertyChanged(); } }
    }
    
    private int _printerBaudRate = 9600;
    /// <summary>
    /// 波特率 (default 9600)
    /// </summary>
    public int PrinterBaudRate
    {
        get => _printerBaudRate;
        set { if (_printerBaudRate != value) { _printerBaudRate = value; OnPropertyChanged(); } }
    }
    
    private string? _receiptHeader;
    /// <summary>
    /// 小票头部文字
    /// </summary>
    public string? ReceiptHeader
    {
        get => _receiptHeader;
        set { if (_receiptHeader != value) { _receiptHeader = value; OnPropertyChanged(); } }
    }
    
    private string? _receiptFooter;
    /// <summary>
    /// 小票尾部文字
    /// </summary>
    public string? ReceiptFooter
    {
        get => _receiptFooter;
        set { if (_receiptFooter != value) { _receiptFooter = value; OnPropertyChanged(); } }
    }

    private string _appTheme = "Default";
    /// <summary>
    /// 主题模式: "Default" (跟随系统, 默认), "Light" (浅色), "Dark" (深色)
    /// </summary>
    public string AppTheme
    {
        get => _appTheme;
        set { if (_appTheme != value) { _appTheme = value; OnPropertyChanged(); } }
    }

    private bool _enablePreviewUpdates = false;
    /// <summary>
    /// 是否开启预览版更新渠道 (接收抢先测试构建，默认 false 关闭)
    /// </summary>
    public bool EnablePreviewUpdates
    {
        get => _enablePreviewUpdates;
        set { if (_enablePreviewUpdates != value) { _enablePreviewUpdates = value; OnPropertyChanged(); } }
    }

    private bool _autoCheckUpdatesOnStartup = true;
    /// <summary>
    /// 是否在启动时自动检测更新 (默认 true 开启)
    /// </summary>
    public bool AutoCheckUpdatesOnStartup
    {
        get => _autoCheckUpdatesOnStartup;
        set { if (_autoCheckUpdatesOnStartup != value) { _autoCheckUpdatesOnStartup = value; OnPropertyChanged(); } }
    }
}
