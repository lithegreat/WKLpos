using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Export;
using WanKePos.WinUI.Messages;

namespace WanKePos.WinUI.ViewModels;

public partial class PurchaseCartItem : ObservableObject
{
    public int ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? SaleUnit { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private decimal _costPrice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private decimal _quantity = 1;

    public decimal Subtotal => CostPrice * Quantity;
}

public partial class PurchaseOrderViewModel : ObservableObject
{
    private readonly IPurchaseOrderRepository _purchaseRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly PurchaseOrderExporter _exporter;

    public ObservableCollection<PurchaseOrder> PurchaseOrders { get; } = new();
    public ObservableCollection<PurchaseCartItem> CartItems { get; } = new();
    public ObservableCollection<Product> AvailableProducts { get; } = new();

    [ObservableProperty]
    private string _searchProductKeyword = string.Empty;

    [ObservableProperty]
    private string _supplierInput = string.Empty;

    [ObservableProperty]
    private string _remarkInput = string.Empty;

    [ObservableProperty]
    private decimal _draftTotalQuantity;

    [ObservableProperty]
    private decimal _draftTotalAmount;

    [ObservableProperty]
    private PurchaseOrder? _selectedOrder;

    [ObservableProperty]
    private int _selectedTabIndex; // 0 = 采购单列表, 1 = 新建采购单

    public Action<string, string>? ShowMessage { get; set; }
    public Func<string, string, Task<bool>>? RequestConfirm { get; set; }
    public Func<string, Task<string?>>? RequestSaveFileDialog { get; set; }

    public PurchaseOrderViewModel(
        IPurchaseOrderRepository purchaseRepo,
        IProductRepository productRepo,
        ISettingsRepository settingsRepo,
        PurchaseOrderExporter exporter)
    {
        _purchaseRepo = purchaseRepo;
        _productRepo = productRepo;
        _settingsRepo = settingsRepo;
        _exporter = exporter;

        WeakReferenceMessenger.Default.Register<ProductsChangedMessage>(this, async (r, m) =>
        {
            if (m.DeletedProductId.HasValue)
            {
                var toRemove = AvailableProducts.FirstOrDefault(p => p.Id == m.DeletedProductId.Value);
                if (toRemove != null)
                {
                    AvailableProducts.Remove(toRemove);
                }
                var cartToRemove = CartItems.FirstOrDefault(c => c.ProductId == m.DeletedProductId.Value);
                if (cartToRemove != null)
                {
                    CartItems.Remove(cartToRemove);
                    RecalculateDraftTotals();
                }
            }
            if (_isInitialized)
            {
                await SearchProductsAsync();
            }
        });
    }

    private bool _isInitialized;

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadOrdersAsync();
        await SearchProductsAsync();
    }

    [RelayCommand]
    public async Task LoadOrdersAsync()
    {
        var orders = await _purchaseRepo.GetAllAsync();
        PurchaseOrders.Clear();
        foreach (var o in orders)
        {
            PurchaseOrders.Add(o);
        }
    }

    [RelayCommand]
    public async Task SearchProductsAsync()
    {
        var products = string.IsNullOrWhiteSpace(SearchProductKeyword)
            ? await _productRepo.GetAllAsync()
            : await _productRepo.SearchAsync(SearchProductKeyword);

        AvailableProducts.Clear();
        foreach (var p in products.Take(100))
        {
            AvailableProducts.Add(p);
        }
    }

    public void AddToCart(Product product)
    {
        var existing = CartItems.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity += 1;
        }
        else
        {
            CartItems.Add(new PurchaseCartItem
            {
                ProductId = product.Id,
                Barcode = product.Barcode,
                ProductName = product.Name,
                Specification = product.Specification,
                SaleUnit = product.SaleUnit,
                CostPrice = product.CostPrice,
                Quantity = 1
            });
        }
        RecalculateDraftTotals();
    }

    public void RemoveFromCart(PurchaseCartItem item)
    {
        CartItems.Remove(item);
        RecalculateDraftTotals();
    }

    public void IncreaseQuantity(PurchaseCartItem item)
    {
        item.Quantity += 1;
        RecalculateDraftTotals();
    }

    public void DecreaseQuantity(PurchaseCartItem item)
    {
        if (item.Quantity > 1)
        {
            item.Quantity -= 1;
        }
        else
        {
            CartItems.Remove(item);
        }
        RecalculateDraftTotals();
    }

    public void RecalculateDraftTotals()
    {
        DraftTotalQuantity = CartItems.Sum(i => i.Quantity);
        DraftTotalAmount = CartItems.Sum(i => i.Subtotal);
    }

    [RelayCommand]
    public void ClearDraft()
    {
        CartItems.Clear();
        SupplierInput = string.Empty;
        RemarkInput = string.Empty;
        RecalculateDraftTotals();
    }

    [RelayCommand]
    public async Task CreatePurchaseOrderAsync()
    {
        if (CartItems.Count == 0)
        {
            ShowMessage?.Invoke("提示", "请先从左侧商品库添加需要采购的商品！");
            return;
        }

        var order = new PurchaseOrder
        {
            Supplier = SupplierInput?.Trim(),
            Remark = RemarkInput?.Trim(),
            Items = CartItems.Select(i => new PurchaseOrderItem
            {
                ProductId = i.ProductId,
                Barcode = i.Barcode,
                ProductName = i.ProductName,
                Specification = i.Specification,
                SaleUnit = i.SaleUnit,
                CostPrice = i.CostPrice,
                Quantity = i.Quantity,
                Subtotal = i.Subtotal
            }).ToList()
        };

        await _purchaseRepo.CreateAsync(order);
        ShowMessage?.Invoke("创建成功", $"采购单 {order.PurchaseOrderNo} 已生成！");
        
        ClearDraft();
        await LoadOrdersAsync();
        SelectedTabIndex = 0; // 自动切回采购单列表
    }

    [RelayCommand]
    public async Task StockInAsync(PurchaseOrder order)
    {
        if (order == null) return;
        if (order.Status == PurchaseOrderStatus.Received)
        {
            ShowMessage?.Invoke("提示", "该采购单已经入库，无需重复操作。");
            return;
        }

        if (RequestConfirm != null)
        {
            var confirm = await RequestConfirm.Invoke("确认入库", $"确认将采购单【{order.PurchaseOrderNo}】共 {order.TotalQuantity} 件商品一键累加到当前实际库存吗？");
            if (!confirm) return;
        }

        var success = await _purchaseRepo.StockInAsync(order.Id);
        if (success)
        {
            ShowMessage?.Invoke("入库成功", $"采购单 {order.PurchaseOrderNo} 已完成入库，商品库存已自动更新！");
            await LoadOrdersAsync();
            WeakReferenceMessenger.Default.Send(new ProductsChangedMessage());
        }
        else
        {
            ShowMessage?.Invoke("入库失败", "操作失败，可能单据状态已改变。");
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync(PurchaseOrder order)
    {
        if (order == null) return;

        try
        {
            var defaultFileName = $"zggj_门店商品-批量收货_{order.PurchaseOrderNo}_{(string.IsNullOrEmpty(order.Supplier) ? "通用供货商" : order.Supplier)}_{DateTime.Now:yyyyMMdd}.xlsx";
            string? savePath = null;

            if (RequestSaveFileDialog != null)
            {
                savePath = await RequestSaveFileDialog.Invoke(defaultFileName);
                if (string.IsNullOrEmpty(savePath)) return; // 用户取消
            }

            var settings = await _settingsRepo.GetSettingsAsync();
            var exportedPath = await _exporter.ExportToExcelAsync(order, settings, savePath);

            // 1. 复制文件路径到系统剪贴板
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(exportedPath);
                Clipboard.SetContent(dataPackage);
            }
            catch { }

            // 2. 在浏览器中打开店铺商品管理
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://estore.jd.com/goods/list",
                    UseShellExecute = true
                });
            }
            catch { }

            // 3. 在资源管理器中定位并高亮选中导出的 Excel 文件
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{exportedPath}\"",
                    UseShellExecute = true
                });
            }
            catch { }

            // 4. 提示用户并在弹窗中清晰给出操作指引
            ShowMessage?.Invoke("导出成功",
                $"采购收货单已生成并保存在【我的文档】！\n\n" +
                $"文件路径：\n{exportedPath}\n\n" +
                $"📋 文件路径已自动复制到剪贴板！\n" +
                $"🌐 已为您打开【店铺商品管理 (https://estore.jd.com/goods/list)】。\n" +
                $"📁 已在文件夹中高亮选中该文件。\n\n" +
                $"【后续操作指引】：\n" +
                $"1. 点击网页右上角的【批量操作】按钮；\n" +
                $"2. 点击下拉列表中的【批量收货】；\n" +
                $"3. 在弹窗中点击【点击选择Excel文件】，直接按 Ctrl+V 粘贴文件路径（或拖入文件）即可完成批量收货！");
        }
        catch (Exception ex)
        {
            ShowMessage?.Invoke("导出失败", $"错误: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task DeleteOrderAsync(PurchaseOrder order)
    {
        if (order == null) return;

        if (RequestConfirm != null)
        {
            var confirm = await RequestConfirm.Invoke("确认删除", $"确定要删除采购单【{order.PurchaseOrderNo}】吗？");
            if (!confirm) return;
        }

        await _purchaseRepo.DeleteAsync(order.Id);
        ShowMessage?.Invoke("提示", "采购单已删除。");
        await LoadOrdersAsync();
    }
}
