using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Import;

namespace WanKePos.WinUI.ViewModels
{
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

        public Func<Task<string?>>? RequestOpenFileDialog { get; set; }
        public Action<string, string>? ShowMessage { get; set; }

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
        public async Task FilterByCategoryAsync(string category)
        {
            if (string.IsNullOrEmpty(category) || category == "全部")
            {
                await ReloadAsync();
                return;
            }
            var products = await _productRepository.GetByCategoryAsync(category);
            Products.Clear();
            foreach (var p in products) Products.Add(p);
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
                    }
                    catch (Exception ex)
                    {
                        ShowMessage?.Invoke("导入失败", $"错误: {ex.Message}");
                    }
                }
            }
        }
    }
}
