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

namespace WanKePos.App.ViewModels
{
    public partial class ProductListViewModel : ObservableObject
    {
        private readonly IProductRepository _productRepository;
        private readonly ExcelImporter _excelImporter;

        public ObservableCollection<Product> Products { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        public ProductListViewModel(IProductRepository productRepository, ExcelImporter excelImporter)
        {
            _productRepository = productRepository;
            _excelImporter = excelImporter;
        }

        [RelayCommand]
        public async Task InitializeAsync()
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
        public async Task SearchAsync(string keyword)
        {
            var results = await _productRepository.SearchAsync(keyword);
            Products.Clear();
            foreach (var p in results) Products.Add(p);
        }

        [RelayCommand]
        public async Task FilterByCategory(string category)
        {
            if (category == "全部")
            {
                await InitializeAsync();
                return;
            }
            var products = await _productRepository.GetByCategoryAsync(category);
            Products.Clear();
            foreach (var p in products) Products.Add(p);
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
                    await InitializeAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
