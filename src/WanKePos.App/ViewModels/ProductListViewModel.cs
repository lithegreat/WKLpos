using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Import;

namespace WanKePos.App.ViewModels;

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

    public Func<Task<Product?>>? RequestAddProductDialog { get; set; }

    public ProductListViewModel(IProductRepository productRepository, ExcelImporter excelImporter)
    {
        _productRepository = productRepository;
        _excelImporter = excelImporter;
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

        Categories.Clear();
        Categories.Add("全部");
        var categories = products.Select(p => p.StoreCategory).Distinct().Where(c => !string.IsNullOrEmpty(c));
        foreach (var c in categories) Categories.Add(c!);
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        var results = await _productRepository.SearchAsync(SearchKeyword);
        Products.Clear();
        foreach (var p in results) Products.Add(p);
    }

    [RelayCommand]
    public async Task FilterByCategory(string category)
    {
        SelectedCategory = category;
        if (category == "全部" || string.IsNullOrEmpty(category))
        {
            var all = await _productRepository.GetAllAsync();
            Products.Clear();
            foreach (var p in all) Products.Add(p);
            return;
        }
        var products = await _productRepository.GetByCategoryAsync(category);
        Products.Clear();
        foreach (var p in products) Products.Add(p);
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
                    MessageBox.Show($"商品【{product.Name}】(条码: {product.Barcode}) 已成功保存入库！", "添加成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    await ReloadAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    [RelayCommand]
    public async Task ImportFromExcelAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            Title = "导入商品"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var products = await _excelImporter.ImportProductsAsync(dialog.FileName);
                await _productRepository.ImportFromListAsync(products);
                MessageBox.Show($"成功导入 {products.Count} 个商品。", "导入成功");
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
