using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Hardware;
using WanKePos.Infrastructure.Import;

namespace WanKePos.WinUI.ViewModels
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

        public Func<Task<string?>>? RequestOpenFileDialog { get; set; }
        public Action<string, string>? ShowMessage { get; set; }

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
            ShowMessage?.Invoke("提示", "设置已成功保存！");
        }

        [RelayCommand]
        public void TestPrint()
        {
            if (string.IsNullOrEmpty(Settings.PrinterPort))
            {
                ShowMessage?.Invoke("提示", "请先选择打印机端口");
                return;
            }
            try
            {
                _receiptPrinter.PortName = Settings.PrinterPort;
                _receiptPrinter.BaudRate = Settings.PrinterBaudRate;
                _receiptPrinter.Connect();
                _receiptPrinter.TestPrint();
                _receiptPrinter.Disconnect();
                ShowMessage?.Invoke("提示", "测试打印指令已发送！");
            }
            catch (Exception ex)
            {
                ShowMessage?.Invoke("错误", $"打印机错误: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task ImportProductsAsync()
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
                    }
                    catch (Exception ex)
                    {
                        ShowMessage?.Invoke("错误", $"导入失败: {ex.Message}");
                    }
                }
            }
        }

        [RelayCommand]
        public async Task ImportMembersAsync()
        {
            if (RequestOpenFileDialog != null)
            {
                var filePath = await RequestOpenFileDialog.Invoke();
                if (!string.IsNullOrEmpty(filePath))
                {
                    try
                    {
                        var members = await _excelImporter.ImportMembersAsync(filePath);
                        await _memberRepository.ImportFromListAsync(members);
                        ShowMessage?.Invoke("导入成功", $"成功导入 {members.Count} 个会员。");
                    }
                    catch (Exception ex)
                    {
                        ShowMessage?.Invoke("错误", $"导入失败: {ex.Message}");
                    }
                }
            }
        }
    }
}
