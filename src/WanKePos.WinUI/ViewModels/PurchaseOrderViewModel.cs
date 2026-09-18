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
using WanKePos.Domain.Services;
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
    private string _cartTitle = "待制采购单";

    /// <summary>
    /// 是否正在修改已存在的采购单
    /// </summary>
    [ObservableProperty]
    private bool _isEditingOrder;

    [ObservableProperty]
    private int? _editingOrderId;

    [ObservableProperty]
    private string? _editingOrderNo;

    [ObservableProperty]
    private string _editingBannerText = string.Empty;

    [ObservableProperty]
    private PurchaseOrder? _selectedOrder;

    [ObservableProperty]
    private int _selectedTabIndex; // 0 = 采购单记录与入库, 1 = 快速制作与待制采购单

    [RelayCommand]
    public void GoToOrders() => SelectedTabIndex = 0;

    [RelayCommand]
    public void GoToQuickCreate() => SelectedTabIndex = 1;

    [RelayCommand]
    public void GoToDraft() => SelectedTabIndex = 1;

    /// <summary>
    /// 入库操作进行中标志 (防止重复点击)
    /// </summary>
    [ObservableProperty]
    private bool _isStockingIn;

    public Action<string, string>? ShowMessage { get; set; }
    public Func<string, string, Task<bool>>? RequestConfirm { get; set; }
    public Func<string, string, Task<string?>>? RequestSaveFileDialog { get; set; }
    public Func<Task<AiPurchaseOrderDto?>>? RequestAiImportDialog { get; set; }
    public Func<Task<PurchaseOrderExportType?>>? RequestExportTypeDialog { get; set; }
    public Func<string, PurchaseOrder, PurchaseOrderExportType, Task>? RequestExportSuccessDialog { get; set; }

    [ObservableProperty]
    private string? _lastExportedFilePath;

    [ObservableProperty]
    private string? _lastExportedOrderNo;

    [ObservableProperty]
    private bool _hasExportedOrder;

    [ObservableProperty]
    private string _exportSuccessBarMessage = string.Empty;

    private readonly Dictionary<string, string> _orderExportedPaths = new();

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

    /// <summary>
    /// 获取采购单完整信息（含所有明细商品）
    /// </summary>
    public async Task<PurchaseOrder?> GetOrderDetailsAsync(int orderId)
    {
        return await _purchaseRepo.GetByIdAsync(orderId);
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

        if (IsEditingOrder)
        {
            CartTitle = $"✏️ 修改采购单: {EditingOrderNo} ({CartItems.Count} 种 / {DraftTotalQuantity:0.##} 件)";
        }
        else
        {
            CartTitle = CartItems.Count > 0
                ? $"📋 待制采购单 ({CartItems.Count} 种 / {DraftTotalQuantity:0.##} 件)"
                : "📋 待制采购单";
        }
    }

    [RelayCommand]
    public void ClearDraft()
    {
        CartItems.Clear();
        SupplierInput = string.Empty;
        RemarkInput = string.Empty;
        IsEditingOrder = false;
        EditingOrderId = null;
        EditingOrderNo = null;
        EditingBannerText = string.Empty;
        RecalculateDraftTotals();
    }

    /// <summary>
    /// 开始编辑已有未入库采购单 (载入数据到工作台)
    /// </summary>
    [RelayCommand]
    public async Task BeginEditOrderAsync(PurchaseOrder order)
    {
        if (order == null) return;
        if (order.Status == PurchaseOrderStatus.Received)
        {
            ShowMessage?.Invoke("提示", "已入库的采购单已被锁定，不允许修改。");
            return;
        }
        if (order.Status == PurchaseOrderStatus.Cancelled)
        {
            ShowMessage?.Invoke("提示", "已作废的采购单无法修改。");
            return;
        }

        // 确保拉取完整明细项
        var fullOrder = await GetOrderDetailsAsync(order.Id);
        if (fullOrder == null)
        {
            fullOrder = order;
        }

        if (CartItems.Count > 0 && !IsEditingOrder && RequestConfirm != null)
        {
            var proceed = await RequestConfirm.Invoke("提示", "待制采购单中已有商品，载入修改将清空当前待制单内容，是否继续？");
            if (!proceed) return;
        }

        IsEditingOrder = true;
        EditingOrderId = fullOrder.Id;
        EditingOrderNo = fullOrder.PurchaseOrderNo;
        EditingBannerText = $"正在修改未入库采购单【{fullOrder.PurchaseOrderNo}】";

        SupplierInput = fullOrder.Supplier ?? string.Empty;
        RemarkInput = fullOrder.Remark ?? string.Empty;

        CartItems.Clear();
        if (fullOrder.Items != null)
        {
            foreach (var item in fullOrder.Items)
            {
                CartItems.Add(new PurchaseCartItem
                {
                    ProductId = item.ProductId,
                    Barcode = item.Barcode,
                    ProductName = item.ProductName,
                    Specification = item.Specification,
                    SaleUnit = item.SaleUnit,
                    CostPrice = item.CostPrice,
                    Quantity = item.Quantity,
                    OnItemChanged = RecalculateDraftTotals
                });
            }
        }

        RecalculateDraftTotals();
        SelectedTabIndex = 1; // 切换到制单工作台
    }

    /// <summary>
    /// 保存对未入库采购单的修改
    /// </summary>
    [RelayCommand]
    public async Task SaveOrderEditAsync()
    {
        if (!IsEditingOrder || !EditingOrderId.HasValue)
        {
            ShowMessage?.Invoke("提示", "当前并非处于采购单修改模式。");
            return;
        }

        if (CartItems.Count == 0)
        {
            ShowMessage?.Invoke("提示", "采购单商品明细不能为空！");
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
            Id = EditingOrderId.Value,
            PurchaseOrderNo = EditingOrderNo ?? string.Empty,
            Supplier = SupplierInput?.Trim(),
            Remark = RemarkInput?.Trim(),
            TotalItemsCount = CartItems.Count,
            TotalQuantity = CartItems.Sum(i => i.Quantity),
            TotalAmount = CartItems.Sum(i => i.Subtotal),
            Items = CartItems.Select(i => new PurchaseOrderItem
            {
                PurchaseOrderId = EditingOrderId.Value,
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

        var success = await _purchaseRepo.UpdateAsync(order);
        if (success)
        {
            ShowMessage?.Invoke("修改成功", $"采购单【{EditingOrderNo}】修改已成功保存！");
            ClearDraft();
            await LoadOrdersAsync();
            SelectedTabIndex = 0; // 自动返回列表
        }
        else
        {
            ShowMessage?.Invoke("修改失败", "保存修改失败，该单据可能已被入库或删除。");
        }
    }

    /// <summary>
    /// 取消当前采购单修改模式
    /// </summary>
    [RelayCommand]
    public void CancelOrderEdit()
    {
        ClearDraft();
        SelectedTabIndex = 0;
    }

    [RelayCommand]
    public async Task CreatePurchaseOrderAsync()
    {
        if (IsEditingOrder)
        {
            await SaveOrderEditAsync();
            return;
        }

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
        await ExportExcelWithTypeAsync(order, null);
    }

    public async Task ExportExcelWithTypeAsync(PurchaseOrder order, PurchaseOrderExportType? exportType)
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

            // 若调用方未显式传入导出类型，则弹窗供用户选择
            if (!exportType.HasValue)
            {
                if (RequestExportTypeDialog != null)
                {
                    var chosen = await RequestExportTypeDialog.Invoke();
                    if (!chosen.HasValue) return; // 用户取消选择
                    exportType = chosen.Value;
                }
                else
                {
                    exportType = PurchaseOrderExportType.SystemImport;
                }
            }

            var defaultFileName = PurchaseOrderExporter.GenerateDefaultFileName(order, exportType.Value);
            var defaultFolder = PurchaseOrderExporter.DefaultExportDirectory;
            string? savePath = null;

            if (RequestSaveFileDialog != null)
            {
                savePath = await RequestSaveFileDialog.Invoke(defaultFileName, defaultFolder);
                if (string.IsNullOrEmpty(savePath)) return; // 用户取消
            }

            var settings = await _settingsRepo.GetSettingsAsync();
            var exportedPath = await _exporter.ExportToExcelAsync(order, settings, savePath, exportType.Value);

            // 记录导出信息与状态
            _orderExportedPaths[order.PurchaseOrderNo] = exportedPath;
            LastExportedFilePath = exportedPath;
            LastExportedOrderNo = order.PurchaseOrderNo;
            HasExportedOrder = true;

            var typeTitle = exportType.Value == PurchaseOrderExportType.VendorSimple ? "厂家进货清单" : "系统收货单";
            ExportSuccessBarMessage = $"采购单【{order.PurchaseOrderNo}】{typeTitle} Excel 导出成功！保存路径：{exportedPath}";

            // 复制文件路径到系统剪贴板，方便随时粘贴
            CopyPathToClipboard(exportedPath);

            // 弹出专用对话框
            if (RequestExportSuccessDialog != null)
            {
                await RequestExportSuccessDialog.Invoke(exportedPath, order, exportType.Value);
            }
            else
            {
                ShowMessage?.Invoke("导出成功",
                    $"{typeTitle}已生成并保存在【我的文档\\采购单】！\n\n" +
                    $"文件路径：\n{exportedPath}\n\n" +
                    $"📋 文件路径已自动复制到剪贴板！");
            }
        }
        catch (Exception ex)
        {
            ShowMessage?.Invoke("导出失败", $"错误: {ex.Message}");
        }
    }

    /// <summary>
    /// 手动打开文件资源管理器定位选中导出的 Excel 文件，并同时唤起默认浏览器打开店铺后台
    /// </summary>
    [RelayCommand]
    public void OpenExportLocationAndBrowser(string? filePath)
    {
        var targetPath = !string.IsNullOrWhiteSpace(filePath) ? filePath : LastExportedFilePath;
        CopyPathToClipboard(targetPath);
        OpenBrowserToStore();
        OpenFileLocation(targetPath);
    }

    /// <summary>
    /// 仅手动在文件资源管理器中定位导出的 Excel 文件
    /// </summary>
    [RelayCommand]
    public void OpenExportLocationOnly(string? filePath)
    {
        var targetPath = !string.IsNullOrWhiteSpace(filePath) ? filePath : LastExportedFilePath;
        OpenFileLocation(targetPath);
    }

    /// <summary>
    /// 仅在浏览器中打开店铺商品管理网页
    /// </summary>
    [RelayCommand]
    public void OpenStoreWebsiteOnly()
    {
        OpenBrowserToStore();
    }

    /// <summary>
    /// 复制指定文件路径到系统剪贴板
    /// </summary>
    [RelayCommand]
    public void CopyExportPath(string? filePath)
    {
        var targetPath = !string.IsNullOrWhiteSpace(filePath) ? filePath : LastExportedFilePath;
        if (!string.IsNullOrEmpty(targetPath))
        {
            CopyPathToClipboard(targetPath);
            ShowMessage?.Invoke("提示", $"文件路径已复制到剪贴板：\n{targetPath}");
        }
    }

    public static void CopyPathToClipboard(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(path);
            Clipboard.SetContent(dataPackage);
        }
        catch { }
    }

    public static void OpenBrowserToStore()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://estore.jd.com/goods/list",
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void OpenFileLocation(string? filePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
            }
            else
            {
                var folder = PurchaseOrderExporter.DefaultExportDirectory;
                if (!System.IO.Directory.Exists(folder))
                {
                    System.IO.Directory.CreateDirectory(folder);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folder}\"",
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    public string? GetExportedFilePathForOrder(PurchaseOrder order)
    {
        if (order == null) return null;
        if (_orderExportedPaths.TryGetValue(order.PurchaseOrderNo, out var path) && System.IO.File.Exists(path))
        {
            return path;
        }
        return LocateExportedExcelFile(order);
    }

    public static string? LocateExportedExcelFile(PurchaseOrder order)
    {
        var exportDir = PurchaseOrderExporter.DefaultExportDirectory;
        if (!System.IO.Directory.Exists(exportDir)) return null;

        var pattern = $"*{order.PurchaseOrderNo}*.xlsx";
        try
        {
            var files = System.IO.Directory.GetFiles(exportDir, pattern);
            if (files.Length > 0)
            {
                return files.OrderByDescending(f => new System.IO.FileInfo(f).LastWriteTime).First();
            }
        }
        catch { }

        return null;
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

    public async Task<List<Product>> GetAllProductsAsync()
    {
        var products = await _productRepo.GetAllAsync();
        return products.ToList();
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

        // 2. 匹配已有商品库（未匹配商品不自动建档，跳过并提示用户手动建档）
        var allProducts = (await _productRepo.GetAllAsync()).ToList();
        var barcodeDict = allProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.Barcode))
            .ToDictionary(p => p.Barcode, p => p);
        var nameDict = allProducts
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        int matchedCount = 0;
        var unmatchedItems = new List<string>();

        foreach (var item in dto.Items)
        {
            Product? targetProduct = null;

            // 优先按条码精确匹配
            if (!string.IsNullOrWhiteSpace(item.Barcode) && barcodeDict.TryGetValue(item.Barcode, out var byBarcode))
            {
                targetProduct = byBarcode;
                matchedCount++;
            }
            // 其次按商品名称完全匹配
            else if (!string.IsNullOrWhiteSpace(item.Name) && nameDict.TryGetValue(item.Name, out var byName))
            {
                targetProduct = byName;
                matchedCount++;
            }
            // 再次通过 ProductMatcher 智能模糊匹配（多栏色号、品牌别名、规格代码）
            else
            {
                var matched = ProductMatcher.Match(item, allProducts);
                if (matched != null)
                {
                    targetProduct = matched;
                    matchedCount++;
                }
            }

            if (targetProduct == null)
            {
                // 本地商品库未匹配到该商品，跳过（不自动建档，由用户手动在商品管理中添加）
                unmatchedItems.Add(item.Name);
                continue;
            }

            // 进价决策：如果目标商品在商品库中有真实进价（>0），优先沿用商品库内的真实进价；
            // 只有当商品库内进价为0且单据中明确有进价时，才使用单据进价
            decimal resolvedCostPrice = targetProduct.CostPrice > 0 
                ? targetProduct.CostPrice 
                : (item.CostPrice > 0 ? item.CostPrice : 0);

            // 加入待制采购明细 (CartItems)
            var existingCartItem = CartItems.FirstOrDefault(c => c.ProductId == targetProduct.Id);
            if (existingCartItem != null)
            {
                existingCartItem.Quantity += item.Quantity;
                if (resolvedCostPrice > 0)
                {
                    existingCartItem.CostPrice = resolvedCostPrice;
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
                    CostPrice = resolvedCostPrice,
                    Quantity = item.Quantity > 0 ? item.Quantity : 1,
                    OnItemChanged = RecalculateDraftTotals
                });
            }
        }

        RecalculateDraftTotals();

        // 切换到【制作采购单】工作台（快速制作与待制采购单同屏显示）
        SelectedTabIndex = 1;

        var importedCount = matchedCount;
        var unmatchedInfo = unmatchedItems.Count > 0
            ? $"• 未匹配商品 (已跳过): {unmatchedItems.Count} 个\n" +
              $"  → {string.Join("、", unmatchedItems.Take(10))}" +
              (unmatchedItems.Count > 10 ? $" 等共 {unmatchedItems.Count} 个" : "") + "\n"
            : "";

        ShowMessage?.Invoke(unmatchedItems.Count > 0 ? "AI 单据导入完成（部分商品未匹配）" : "AI 单据导入成功",
            $"已成功导入 {importedCount} / {dto.Items.Count} 项采购商品！\n" +
            $"• 库中已匹配商品: {matchedCount} 个\n" +
            unmatchedInfo +
            $"• 当前采购总件数: {DraftTotalQuantity:0.##} 件\n" +
            $"• 当前采购总金额: ¥{DraftTotalAmount:F2}\n\n" +
            (unmatchedItems.Count > 0
                ? "⚠️ 未匹配的商品请先在【商品管理】中手动建档，然后重新导入即可自动匹配。\n\n"
                : "") +
            "明细已载入待制采购单工作台，请核实数量及进价后点击【💾 生成采购单】。");
    }
}
