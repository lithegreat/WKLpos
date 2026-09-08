namespace WanKePos.Domain.Entities;

/// <summary>
/// 门店设置实体
/// </summary>
public class StoreSettings
{
    public int Id { get; set; }
    
    /// <summary>
    /// 门店名称
    /// </summary>
    public string StoreName { get; set; } = string.Empty;
    
    /// <summary>
    /// 门店地址
    /// </summary>
    public string? StoreAddress { get; set; }
    
    /// <summary>
    /// 门店电话
    /// </summary>
    public string? StorePhone { get; set; }
    
    /// <summary>
    /// 每消费N元获1积分 (default 1)
    /// </summary>
    public decimal PointsPerYuan { get; set; } = 1m;
    
    /// <summary>
    /// 打印机端口
    /// </summary>
    public string? PrinterPort { get; set; }
    
    /// <summary>
    /// 波特率 (default 9600)
    /// </summary>
    public int PrinterBaudRate { get; set; } = 9600;
    
    /// <summary>
    /// 小票头部文字
    /// </summary>
    public string? ReceiptHeader { get; set; }
    
    /// <summary>
    /// 小票尾部文字
    /// </summary>
    public string? ReceiptFooter { get; set; }

    /// <summary>
    /// 主题模式: "Default" (跟随系统, 默认), "Light" (浅色), "Dark" (深色)
    /// </summary>
    public string AppTheme { get; set; } = "Default";
}
