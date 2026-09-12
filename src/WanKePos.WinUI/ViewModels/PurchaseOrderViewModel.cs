using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using Windows.ApplicationModel.DataTransfer;
using WanKePos.Domain;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;
using WanKePos.Domain.Models;
using WanKePos.Infrastructure.Export;
using WanKePos.WinUI.Messages;
using WanKePos.WinUI.Models;

namespace WanKePos.WinUI.ViewModels;

public partial class PurchaseOrderViewModel : ObservableObject
{
    private readonly IPurchaseOrderRepository _purchaseRepo;
    private readonly IProductRepository _productRepo;
    private readonly ISettingsRepository _settingsRepo;
    private readonly PurchaseOrderExporter _exporter;

    public ObservableCollection<PurchaseOrder> PurchaseOrders { get; } = new();
    public ObservableCollection<PurchaseCartItem> CartItems { get; } = new();
    public ObservableCollection<Product> AvailableProducts { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty]
    private string _searchProductKeyword = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = CategoryConstants.All;

    [ObservableProperty]
    private int _availableProductCount;

    [ObservableProperty]
    private string _supplierInput = string.Empty;

    [ObservableProperty]
    private string _remarkInput = string.Empty;

    [ObservableProperty]
    private decimal _draftTotalQuantity;

    [ObservableProperty]
    private decimal _draftTotalAmount;

    /// <summary>
    /// 购物车动态标题 (包含品类数与总件数徽标)
    /// </summary>
    [ObservableProperty]
    private string _cartTitle = "📋 待制采购单";

    [ObservableProperty]
    private PurchaseOrder? _selectedOrder;

    [ObservableProperty]
    private int _selectedTabIndex; // 0 = 采购单列表, 1 = 新建采购单

    /// <summary>
    /// 入库操作进行中标志 (防止重复点击)
    /// </summary>
    [ObservableProperty]
    private bool _isStockingIn;

    public Action<string, string>? ShowMessage { get; set; }
    public Func<string, string, Task<bool>>? RequestConfirm { get; set; }
    public Func<string, string, Task<string?>>? RequestSaveFileDialog { get; set; }
    public Func<Task<AiPurchaseOrderDto?>>? RequestAiImportDialog { get; set; }

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
                await LoadCategoriesAsync();
                await RefreshAvailableProductsAsync();
            }
        });

        CartItems.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (PurchaseCartItem item in e.NewItems)
                {
                    item.OnItemChanged = RecalculateDraftTotals;
                }
            }
            RecalculateDraftTotals();
        };
    }

    private bool _isInitialized;

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadOrdersAsync();
        await LoadCategoriesAsync();
        await RefreshAvailableProductsAsync();
    }

    public async Task LoadCategoriesAsync()
    {
        var categories = await _productRepo.GetCategoriesAsync();
        Categories.Clear();
        Categories.Add(CategoryConstants.All);
        foreach (var c in categories)
        {
            if (!string.IsNullOrWhiteSpace(c))
            {
                Categories.Add(c);
            }
        }
    }

    [RelayCommand]
    public async Task FilterByCategoryAsync(string category)
    {
        SelectedCategory = category;
        SearchProductKeyword = string.Empty;
        await RefreshAvailableProductsAsync();
    }

    [RelayCommand]
    public async Task ResetProductSearchAsync()
    {
        SelectedCategory = CategoryConstants.All;
        SearchProductKeyword = string.Empty;
        await RefreshAvailableProductsAsync();
    }

    [RelayCommand]
    public async Task LoadOrdersAsync()
    {
        var orders = await _purchaseRepo.GetAllSummaryAsync();
        PurchaseOrders.Clear();
        foreach (var o in orders)
        {
            PurchaseOrders.Add(o);
        }
    }

    [RelayCommand]
    public async Task SearchProductsAsync()
    {
        await RefreshAvailableProductsAsync();
    }

    public async Task RefreshAvailableProductsAsync()
    {
        List<Product> products;
        if (!string.IsNullOrWhiteSpace(SearchProductKeyword))
        {
            var searchResults = await _productRepo.SearchAsync(SearchProductKeyword.Trim());
            if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != CategoryConstants.All)
            {
                products = searchResults.Where(p => p.StoreCategory == SelectedCategory).ToList();
            }
            else
            {
                products = searchResults;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(SelectedCategory) || SelectedCategory == CategoryConstants.All)
            {
                products = await _productRepo.GetAllAsync();
            }
            else
            {
                products = await _productRepo.GetByCategoryAsync(SelectedCategory);
            }
        }

        // 批量替换集合内容，减少 UI CollectionChanged 通知次数
        AvailableProducts.Clear();
        foreach (var p in products)
        {
            AvailableProducts.Add(p);
        }
        AvailableProductCount = products.Count;
    }

    public void AddToCart(Product product)
    {
        // 如果当前尚未填写供货商，且该商品有记录供货商，则自动填入供货商名称方便制单
        if (string.IsNullOrWhiteSpace(SupplierInput) && !string.IsNullOrWhiteSpace(product.Supplier))
        {
            SupplierInput = product.Supplier;
        }

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
                Quantity = 1,
                OnItemChanged = RecalculateDraftTotals
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
        CartTitle = CartItems.Count > 0
            ? $"📋 待制采购单 ({CartItems.Count} 种 / {DraftTotalQuantity:0.##} 件)"
            : "📋 待制采购单";
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

        foreach (var item in CartItems)
        {
            if (item.Quantity <= 0)
            {
                ShowMessage?.Invoke("提示", $"商品【{item.ProductName}】采购数量必须大于0！");
                return;
            }
            if (item.CostPrice < 0)
            {
                ShowMessage?.Invoke("提示", $"商品【{item.ProductName}】采购进价不能为负数！");
                return;
            }
        }

        RecalculateDraftTotals();

        var order = new PurchaseOrder
        {
            Supplier = SupplierInput?.Trim(),
            Remark = RemarkInput?.Trim(),
            TotalItemsCount = CartItems.Count,
            TotalQuantity = CartItems.Sum(i => i.Quantity),
            TotalAmount = CartItems.Sum(i => i.Subtotal),
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
        if (order == null || IsStockingIn) return;
        if (order.Status == PurchaseOrderStatus.Received)
        {
            ShowMessage?.Invoke("提示", "该采购单已经入库，无需重复操作。");
            return;
        }
        if (order.Status == PurchaseOrderStatus.Cancelled)
        {
            ShowMessage?.Invoke("提示", "该采购单已取消，无法入库。");
            return;
        }

        if (RequestConfirm != null)
        {
            var confirm = await RequestConfirm.Invoke("确认入库", $"确认将采购单【{order.PurchaseOrderNo}】共 {order.TotalQuantity} 件商品一键累加到当前实际库存吗？");
            if (!confirm) return;
        }

        IsStockingIn = true;
        try
        {
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
        finally
        {
            IsStockingIn = false;
        }
    }

    [RelayCommand]
    public async Task ExportExcelAsync(PurchaseOrder order)
    {
        if (order == null) return;

        try
        {
            // 确保单据已加载商品明细（当从采购单列表传入时，order.Items 为空，需从数据库加载完整单据）
            if (order.Items == null || order.Items.Count == 0)
            {
                var fullOrder = await _purchaseRepo.GetByIdAsync(order.Id);
                if (fullOrder != null)
                {
                    order = fullOrder;
                }
            }

            if (order.Items == null || order.Items.Count == 0)
            {
                ShowMessage?.Invoke("提示", $"采购单【{order.PurchaseOrderNo}】没有包含任何商品明细，无法导出。");
                return;
            }

            var defaultFileName = $"zggj_门店商品-批量收货_{order.PurchaseOrderNo}_{(string.IsNullOrEmpty(order.Supplier) ? "通用供货商" : order.Supplier)}_{DateTime.Now:yyyyMMdd}.xlsx";
            var defaultFolder = PurchaseOrderExporter.DefaultExportDirectory;
            string? savePath = null;

            if (RequestSaveFileDialog != null)
            {
                savePath = await RequestSaveFileDialog.Invoke(defaultFileName, defaultFolder);
                if (string.IsNullOrEmpty(savePath)) return; // 用户取消
            }

            var settings = await _settingsRepo.GetSettingsAsync();
            var exportedPath = await _exporter.ExportToExcelAsync(order, settings, savePath);

            PostExportActions(exportedPath);

            // 提示用户并在弹窗中清晰给出操作指引
            ShowMessage?.Invoke("导出成功",
                $"采购收货单已生成并保存在【我的文档\\采购单】！\n\n" +
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

    /// <summary>
    /// 导出后置协同操作：自动复制路径、唤起管理网页、定位文件并置顶系统窗口
    /// </summary>
    private void PostExportActions(string exportedPath)
    {
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

        // 4. 保持软件窗口在浏览器和文件夹上方
        App.EnsureMainWindowOnTop();
        _ = Task.Run(async () =>
        {
            await Task.Delay(400);
            App.EnsureMainWindowOnTop();
            await Task.Delay(800);
            App.EnsureMainWindowOnTop();
        });
    }

    [RelayCommand]
    public async Task DeleteOrderAsync(PurchaseOrder order)
    {
        if (order == null) return;

        if (order.Status == PurchaseOrderStatus.Received)
        {
            ShowMessage?.Invoke("提示", "已入库的采购单不允许删除，库存数据将无法追溯。");
            return;
        }

        if (RequestConfirm != null)
        {
            var confirm = await RequestConfirm.Invoke("确认删除", $"确定要删除采购单【{order.PurchaseOrderNo}】吗？");
            if (!confirm) return;
        }

        try
        {
            await _purchaseRepo.DeleteAsync(order.Id);
            ShowMessage?.Invoke("提示", "采购单已删除。");
            await LoadOrdersAsync();
        }
        catch (InvalidOperationException ex)
        {
            ShowMessage?.Invoke("删除失败", ex.Message);
        }
    }

    [RelayCommand]
    public async Task ImportFromAiAsync()
    {
        if (RequestAiImportDialog == null) return;

        var dto = await RequestAiImportDialog.Invoke();
        if (dto == null || dto.Items.Count == 0) return;

        await ProcessAiImportedOrderAsync(dto);
    }

    public async Task ProcessAiImportedOrderAsync(AiPurchaseOrderDto dto)
    {
        if (dto == null || dto.Items.Count == 0) return;

        // 1. 设置供货商与备注
        if (!string.IsNullOrWhiteSpace(dto.Supplier))
        {
            SupplierInput = dto.Supplier;
        }

        if (!string.IsNullOrWhiteSpace(dto.Remark))
        {
            RemarkInput = dto.Remark;
        }
        else if (string.IsNullOrWhiteSpace(RemarkInput))
        {
            RemarkInput = $"AI拍照识别单据 ({DateTime.Now:yyyy-MM-dd HH:mm})";
        }

        // 2. 匹配已有商品库或自动为新商品建档
        var allProducts = await _productRepo.GetAllAsync();
        var barcodeDict = allProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Barcode))
            .ToDictionary(p => p.Barcode, p => p);
        var nameDict = allProducts
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        int matchedCount = 0;
        int newProductCount = 0;

        foreach (var item in dto.Items)
        {
            Product? targetProduct = null;

            // 优先按条码匹配
            if (!string.IsNullOrWhiteSpace(item.Barcode) && barcodeDict.TryGetValue(item.Barcode, out var byBarcode))
            {
                targetProduct = byBarcode;
                matchedCount++;
            }
            // 其次按商品名称匹配
            else if (!string.IsNullOrWhiteSpace(item.Name) && nameDict.TryGetValue(item.Name, out var byName))
            {
                targetProduct = byName;
                matchedCount++;
            }
            else
            {
                // 本地库未找到该商品：自动在商品库中建档预存，分配条码
                var newBarcode = !string.IsNullOrWhiteSpace(item.Barcode)
                    ? item.Barcode
                    : $"AI{DateTime.Now:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";

                var newProduct = new Product
                {
                    Barcode = newBarcode,
                    Name = item.Name,
                    Specification = item.Specification,
                    SaleUnit = string.IsNullOrWhiteSpace(item.SaleUnit) ? "件" : item.SaleUnit,
                    CostPrice = item.CostPrice,
                    RetailPrice = Math.Round(item.CostPrice * 1.35m, 2), // 默认预设参考售价
                    Stock = 0,
                    StoreCategory = "其他",
                    Supplier = dto.Supplier,
                    SaleMethod = "按件",
                    ProductType = "标品",
                    CreatedAt = DateTime.UtcNow,
                    LastModified = DateTime.UtcNow
                };

                await _productRepo.AddOrUpdateAsync(newProduct);
                targetProduct = await _productRepo.GetByBarcodeAsync(newBarcode) ?? newProduct;

                // 注册到本地字典防止同单内重复项二次创建
                barcodeDict[targetProduct.Barcode] = targetProduct;
                nameDict[targetProduct.Name] = targetProduct;
                newProductCount++;
            }

            // 加入待制采购明细 (CartItems)
            var existingCartItem = CartItems.FirstOrDefault(c => c.ProductId == targetProduct.Id);
            if (existingCartItem != null)
            {
                existingCartItem.Quantity += item.Quantity;
                if (item.CostPrice > 0)
                {
                    existingCartItem.CostPrice = item.CostPrice;
                }
            }
            else
            {
                CartItems.Add(new PurchaseCartItem
                {
                    ProductId = targetProduct.Id,
                    Barcode = targetProduct.Barcode,
                    ProductName = targetProduct.Name,
                    Specification = targetProduct.Specification ?? item.Specification,
                    SaleUnit = targetProduct.SaleUnit ?? item.SaleUnit,
                    CostPrice = item.CostPrice > 0 ? item.CostPrice : targetProduct.CostPrice,
                    Quantity = item.Quantity > 0 ? item.Quantity : 1,
                    OnItemChanged = RecalculateDraftTotals
                });
            }
        }

        RecalculateDraftTotals();

        // 切换到【快速制作采购单】工作台
        SelectedTabIndex = 1;

        if (newProductCount > 0)
        {
            await LoadCategoriesAsync();
            await RefreshAvailableProductsAsync();
            WeakReferenceMessenger.Default.Send(new ProductsChangedMessage());
        }

        ShowMessage?.Invoke("AI 单据导入成功",
            $"已成功导入 {dto.Items.Count} 项采购商品！\n" +
            $"• 库中已匹配商品: {matchedCount} 个\n" +
            $"• 自动建档新商品: {newProductCount} 个\n" +
            $"• 当前采购总件数: {DraftTotalQuantity:0.##} 件\n" +
            $"• 当前采购总金额: ¥{DraftTotalAmount:F2}\n\n" +
            "明细已载入待制采购单工作台，请核实数量及进价后点击【💾 生成采购单】。");
    }
}
