using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Import;
using WanKePos.WinUI.Messages;

namespace WanKePos.WinUI.ViewModels;

public partial class ProductListViewModel : ObservableObject
{
    private readonly IProductRepository _productRepository;
    private readonly ExcelImporter _excelImporter;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<string> Categories { get; } = new();

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "全部";

    [ObservableProperty]
    private int _productCount;

    public Func<Task<string?>>? RequestOpenFileDialog { get; set; }
    public Func<Task<Product?>>? RequestAddProductDialog { get; set; }
    public Func<Product, Task<Product?>>? RequestEditProductDialog { get; set; }
    public Func<string, string, Task<bool>>? RequestConfirm { get; set; }
    public Action<string, string>? ShowMessage { get; set; }

    public ProductListViewModel(IProductRepository productRepository, ExcelImporter excelImporter)
    {
        _productRepository = productRepository;
        _excelImporter = excelImporter;

        WeakReferenceMessenger.Default.Register<ProductsChangedMessage>(this, async (r, m) =>
        {
            // 如果是其他模块导致的商品变更（如采购入库、后台导入等），且当前已初始化，则重新加载
            if (_isInitialized)
            {
                await ReloadAsync();
            }
        });
    }

    private bool _isInitialized;

    [RelayCommand]
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        var products = await _productRepository.GetAllAsync();
        Products.Clear();
        foreach (var p in products) Products.Add(p);
        ProductCount = Products.Count;

        Categories.Clear();
        Categories.Add(CategoryConstants.All);
        var categories = products.Select(p => p.StoreCategory).Distinct().Where(c => !string.IsNullOrEmpty(c));
        foreach (var c in categories) Categories.Add(c!);
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        var results = await _productRepository.SearchAsync(SearchKeyword);
        Products.Clear();
        foreach (var p in results) Products.Add(p);
        ProductCount = Products.Count;
    }

    [RelayCommand]
    public async Task FilterByCategoryAsync(string category)
    {
        SelectedCategory = category;
        if (string.IsNullOrEmpty(category) || category == CategoryConstants.All)
        {
            var all = await _productRepository.GetAllAsync();
            Products.Clear();
            foreach (var p in all) Products.Add(p);
            ProductCount = Products.Count;
            return;
        }
        var products = await _productRepository.GetByCategoryAsync(category);
        Products.Clear();
        foreach (var p in products) Products.Add(p);
        ProductCount = Products.Count;
    }

    [RelayCommand]
    public async Task AddProductAsync()
    {
        if (RequestAddProductDialog != null)
        {
            var product = await RequestAddProductDialog.Invoke();
            if (product != null)
            {
                try
                {
                    await _productRepository.AddOrUpdateAsync(product);
                    ShowMessage?.Invoke("添加成功", $"商品【{product.Name}】(条码: {product.Barcode}) 已成功保存入库！");
                    await ReloadAsync();
                    WeakReferenceMessenger.Default.Send(new ProductsChangedMessage());
                }
                catch (Exception ex)
                {
                    ShowMessage?.Invoke("保存失败", $"错误: {ex.Message}");
                }
            }
        }
    }

    [RelayCommand]
    public async Task EditProductAsync(Product product)
    {
        if (product == null) return;

        if (RequestEditProductDialog != null)
        {
            var updated = await RequestEditProductDialog.Invoke(product);
            if (updated != null)
            {
                try
                {
                    await _productRepository.UpdateAsync(updated);
                    product.CopyFrom(updated); // 内存对象即时同步并触发 INotifyPropertyChanged
                    ShowMessage?.Invoke("修改成功", $"商品【{updated.Name}】(条码: {updated.Barcode}) 信息已成功更新！");
                    await RefreshCurrentViewAsync();
                    WeakReferenceMessenger.Default.Send(new ProductsChangedMessage());
                }
                catch (Exception ex)
                {
                    ShowMessage?.Invoke("修改失败", $"错误: {ex.Message}");
                }
            }
        }
    }

    public async Task RefreshCurrentViewAsync()
    {
        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            await SearchAsync();
        }
        else if (!string.IsNullOrWhiteSpace(SelectedCategory) && SelectedCategory != CategoryConstants.All)
        {
            await FilterByCategoryAsync(SelectedCategory);
        }
        else
        {
            await ReloadAsync();
        }
    }

    [RelayCommand]
    public async Task ImportFromExcelAsync()
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
                    await ReloadAsync();
                    WeakReferenceMessenger.Default.Send(new ProductsChangedMessage());
                }
                catch (Exception ex)
                {
                    ShowMessage?.Invoke("导入失败", $"错误: {ex.Message}");
                }
            }
        }
    }

    [RelayCommand]
    public async Task DeleteProductAsync(Product product)
    {
        if (product == null) return;

        if (RequestConfirm != null)
        {
            var confirm = await RequestConfirm.Invoke("确认删除商品", $"确认删除商品【{product.Name}】(条码: {product.Barcode}, 当前库存: {product.Stock}) 吗？\n删除后该商品数据将永久清除。");
            if (!confirm) return;
        }

        try
        {
            await _productRepository.DeleteAsync(product.Id);
            ShowMessage?.Invoke("删除成功", $"商品【{product.Name}】已成功删除。");
            await ReloadAsync();
            WeakReferenceMessenger.Default.Send(new ProductsChangedMessage(product.Id, product.Barcode));
        }
        catch (Exception ex)
        {
            ShowMessage?.Invoke("删除失败", $"错误: {ex.Message}");
        }
    }
}
