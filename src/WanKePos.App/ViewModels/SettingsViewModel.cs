using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Hardware;
using WanKePos.Infrastructure.Import;

namespace WanKePos.App.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly ReceiptPrinter _receiptPrinter;
        private readonly IProductRepository _productRepository;
        private readonly IMemberRepository _memberRepository;
        private readonly ExcelImporter _excelImporter;

        [ObservableProperty]
        private StoreSettings _settings = new();

        public ObservableCollection<string> AvailablePorts { get; } = new();

        public SettingsViewModel(
            ISettingsRepository settingsRepository,
            ReceiptPrinter receiptPrinter,
            IProductRepository productRepository,
            IMemberRepository memberRepository,
            ExcelImporter excelImporter)
        {
            _settingsRepository = settingsRepository;
            _receiptPrinter = receiptPrinter;
            _productRepository = productRepository;
            _memberRepository = memberRepository;
            _excelImporter = excelImporter;
        }

        private bool _isInitialized;

        [RelayCommand]
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            Settings = await _settingsRepository.GetSettingsAsync() ?? new StoreSettings();
            
            AvailablePorts.Clear();
            var ports = SerialPort.GetPortNames();
            foreach (var p in ports) AvailablePorts.Add(p);
        }

        [RelayCommand]
        public async Task SaveSettingsAsync()
        {
            await _settingsRepository.SaveSettingsAsync(Settings);
            MessageBox.Show("设置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        public void TestPrint()
        {
            if (string.IsNullOrEmpty(Settings.PrinterPort))
            {
                MessageBox.Show("请先选择打印机端口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                _receiptPrinter.PortName = Settings.PrinterPort;
                _receiptPrinter.BaudRate = Settings.PrinterBaudRate;
                _receiptPrinter.Connect();
                _receiptPrinter.TestPrint();
                _receiptPrinter.Disconnect();
                MessageBox.Show("测试打印已发送", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打印机错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task ImportProductsAsync()
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
                    MessageBox.Show($"成功导入 {products.Count} 个商品。", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        public async Task ImportMembersAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel Files (*.xlsx)|*.xlsx",
                Title = "导入会员"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var members = await _excelImporter.ImportMembersAsync(dialog.FileName);
                    await _memberRepository.ImportFromListAsync(members);
                    MessageBox.Show($"成功导入 {members.Count} 个会员。", "导入成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
