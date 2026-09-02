using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Export;

namespace WanKePos.App.ViewModels;

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
    private int _selectedTabIndex; // 0 = 采购单列表, 1 = 新建采购单

    public Action<string, string>? ShowMessage { get; set; }
    public Func<string, string, Task<bool>>? RequestConfirm { get; set; }
    public Func<string, string?>? RequestSaveFileDialog { get; set; }

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
        SelectedTabIndex = 0;
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
            var defaultFileName = $"采购订单_{order.PurchaseOrderNo}_{(string.IsNullOrEmpty(order.Supplier) ? "通用供货商" : order.Supplier)}_{DateTime.Now:yyyyMMdd}.xlsx";
            string? savePath = null;

            if (RequestSaveFileDialog != null)
            {
                savePath = RequestSaveFileDialog.Invoke(defaultFileName);
                if (string.IsNullOrEmpty(savePath)) return;
            }

            var settings = await _settingsRepo.GetSettingsAsync();
            var exportedPath = await _exporter.ExportToExcelAsync(order, settings, savePath);
            ShowMessage?.Invoke("导出成功", $"采购单 Excel 文件已成功导出至：\n{exportedPath}\n可以随时通过微信或邮件发送给供货商。");
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
