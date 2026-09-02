using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Hardware;

namespace WanKePos.App.ViewModels
{
    /// <summary>
    /// 购物车项
    /// </summary>
    public partial class CartItem : ObservableObject
    {
        public int ProductId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Subtotal))]
        private decimal _quantity;

        [ObservableProperty]
        private decimal _unitPrice;

        [ObservableProperty]
        private decimal _memberPrice;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Subtotal))]
        private decimal _actualPrice;

        public decimal Subtotal => Quantity * ActualPrice;
        public string SaleUnit { get; set; } = string.Empty;
    }

    /// <summary>
    /// 收银台核心 ViewModel
    /// </summary>
    public partial class CashierViewModel : ObservableObject
    {
        private readonly IProductRepository _productRepo;
        private readonly IMemberRepository _memberRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly ISettingsRepository _settingsRepo;
        private readonly ReceiptPrinter _printer;

        public ObservableCollection<CartItem> CartItems { get; } = new();
        public ObservableCollection<Product> DisplayProducts { get; } = new();
        public ObservableCollection<string> Categories { get; } = new();

        [ObservableProperty]
        private string _barcodeInput = string.Empty;

        [ObservableProperty]
        private string _memberPhoneInput = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMemberMode))]
        [NotifyPropertyChangedFor(nameof(MemberDisplayText))]
        private Member? _currentMember;

        public bool IsMemberMode => CurrentMember != null;

        public string MemberDisplayText => CurrentMember != null
            ? $"{CurrentMember.Name} | 积分: {CurrentMember.TotalPoints} | 余额: ¥{CurrentMember.Balance:F2}"
            : "";

        [ObservableProperty]
        private decimal _subTotal;

        [ObservableProperty]
        private decimal _discountAmount;

        [ObservableProperty]
        private decimal _payableAmount;

        [ObservableProperty]
        private string? _selectedCategory;

        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        public CashierViewModel(
            IProductRepository productRepo,
            IMemberRepository memberRepo,
            IOrderRepository orderRepo,
            ISettingsRepository settingsRepo,
            ReceiptPrinter printer)
        {
            _productRepo = productRepo;
            _memberRepo = memberRepo;
            _orderRepo = orderRepo;
            _settingsRepo = settingsRepo;
            _printer = printer;
        }

        /// <summary>
        /// 页面加载时初始化数据
        /// </summary>
        [RelayCommand]
        private async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            await LoadProductsAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            var categories = await _productRepo.GetCategoriesAsync();
            Categories.Clear();
            Categories.Add("全部");
            foreach (var cat in categories)
                Categories.Add(cat);
            SelectedCategory = "全部";
        }

        private async Task LoadProductsAsync()
        {
            var products = string.IsNullOrWhiteSpace(SelectedCategory) || SelectedCategory == "全部"
                ? await _productRepo.GetAllAsync()
                : await _productRepo.GetByCategoryAsync(SelectedCategory);

            DisplayProducts.Clear();
            foreach (var p in products.Where(p => p.Stock > 0))
                DisplayProducts.Add(p);
        }

        /// <summary>
        /// 扫码/搜索 — 扫码枪扫入或手动输入后按回车触发
        /// </summary>
        [RelayCommand]
        private async Task SearchOrScanAsync()
        {
            if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

            // 先按条码精确匹配
            var product = await _productRepo.GetByBarcodeAsync(BarcodeInput.Trim());
            if (product != null)
            {
                AddToCart(product);
            }
            else
            {
                // 按名称模糊搜索，如果只有一个结果直接加入购物车
                var results = await _productRepo.SearchAsync(BarcodeInput.Trim());
                if (results.Count == 1)
                {
                    AddToCart(results[0]);
                }
                else if (results.Count > 1)
                {
                    DisplayProducts.Clear();
                    foreach (var p in results)
                        DisplayProducts.Add(p);
                }
                else
                {
                    MessageBox.Show($"未找到商品: {BarcodeInput}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            BarcodeInput = string.Empty;
        }

        [RelayCommand]
        private void AddToCart(Product p)
        {
            var existing = CartItems.FirstOrDefault(x => x.ProductId == p.Id);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                var price = IsMemberMode && p.MemberPrice.HasValue && p.MemberPrice.Value > 0
                    ? p.MemberPrice.Value
                    : p.RetailPrice;
                CartItems.Add(new CartItem
                {
                    ProductId = p.Id,
                    Barcode = p.Barcode,
                    ProductName = p.Name,
                    Quantity = 1,
                    UnitPrice = p.RetailPrice,
                    MemberPrice = p.MemberPrice ?? 0,
                    ActualPrice = price,
                    SaleUnit = p.SaleUnit ?? ""
                });
            }
            RecalculateTotals();
        }

        [RelayCommand]
        private void RemoveFromCart(CartItem item)
        {
            CartItems.Remove(item);
            RecalculateTotals();
        }

        [RelayCommand]
        private void IncreaseQuantity(CartItem item)
        {
            item.Quantity++;
            RecalculateTotals();
        }

        [RelayCommand]
        private void DecreaseQuantity(CartItem item)
        {
            if (item.Quantity > 1)
                item.Quantity--;
            else
                CartItems.Remove(item);
            RecalculateTotals();
        }

        /// <summary>
        /// 查找会员
        /// </summary>
        [RelayCommand]
        private async Task SearchMemberAsync()
        {
            if (string.IsNullOrWhiteSpace(MemberPhoneInput)) return;

            var member = await _memberRepo.GetByPhoneAsync(MemberPhoneInput.Trim());
            if (member != null && member.Status == "正常")
            {
                CurrentMember = member;
                // 切换所有购物车商品到会员价
                foreach (var item in CartItems)
                {
                    if (item.MemberPrice > 0)
                        item.ActualPrice = item.MemberPrice;
                }
                RecalculateTotals();
            }
            else
            {
                MessageBox.Show(member?.Status == "禁用" ? "该会员已被禁用" : "未找到该会员",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        [RelayCommand]
        private void ClearMember()
        {
            CurrentMember = null;
            MemberPhoneInput = string.Empty;
            // 切换回零售价
            foreach (var item in CartItems)
                item.ActualPrice = item.UnitPrice;
            RecalculateTotals();
        }

        [RelayCommand]
        private void ClearCart()
        {
            CartItems.Clear();
            RecalculateTotals();
        }

        [RelayCommand]
        private async Task SelectCategoryAsync(string category)
        {
            SelectedCategory = category;
            await LoadProductsAsync();
        }

        /// <summary>
        /// 现金结算
        /// </summary>
        [RelayCommand]
        private async Task CheckoutCashAsync()
        {
            if (!CartItems.Any()) return;

            // 弹出输入框获取实收金额
            var inputDialog = new Dialogs.CheckoutDialog(PayableAmount, "现金支付");
            if (inputDialog.ShowDialog() == true)
            {
                var paidAmount = inputDialog.PaidAmount;
                var changeAmount = paidAmount - PayableAmount;

                await ProcessCheckoutAsync(PaymentMethod.Cash, paidAmount, changeAmount);

                MessageBox.Show($"结算成功！\n实收: ¥{paidAmount:F2}\n找零: ¥{changeAmount:F2}",
                    "✅ 交易完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 会员余额结算
        /// </summary>
        [RelayCommand]
        private async Task CheckoutBalanceAsync()
        {
            if (!CartItems.Any() || !IsMemberMode) return;

            if (CurrentMember!.Balance < PayableAmount)
            {
                MessageBox.Show($"会员余额不足！\n当前余额: ¥{CurrentMember.Balance:F2}\n应付: ¥{PayableAmount:F2}",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"确认使用会员余额支付？\n会员: {CurrentMember.Name}\n余额: ¥{CurrentMember.Balance:F2}\n应付: ¥{PayableAmount:F2}",
                "余额支付确认", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await ProcessCheckoutAsync(PaymentMethod.MemberBalance, PayableAmount, 0);

                // 扣减会员余额
                await _memberRepo.UpdateBalanceAsync(CurrentMember.Id, -PayableAmount);
                CurrentMember.Balance -= PayableAmount;

                MessageBox.Show($"余额支付成功！\n剩余余额: ¥{CurrentMember.Balance:F2}",
                    "✅ 交易完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task ProcessCheckoutAsync(PaymentMethod paymentMethod, decimal paidAmount, decimal changeAmount)
        {
            var settings = await _settingsRepo.GetSettingsAsync();

            // 计算积分
            decimal pointsEarned = 0;
            if (settings.PointsPerYuan > 0)
            {
                pointsEarned = Math.Floor(PayableAmount / settings.PointsPerYuan);
            }

            // 创建订单
            var order = new Order
            {
                MemberId = CurrentMember?.Id,
                MemberName = CurrentMember?.Name,
                MemberPhone = CurrentMember?.Phone,
                TotalAmount = SubTotal,
                DiscountAmount = DiscountAmount,
                PayableAmount = PayableAmount,
                PaidAmount = paidAmount,
                ChangeAmount = changeAmount,
                PaymentMethod = paymentMethod,
                PointsEarned = pointsEarned,
                Status = OrderStatus.Normal,
                SyncStatus = SyncStatus.Pending,
                Items = CartItems.Select(ci => new OrderItem
                {
                    ProductId = ci.ProductId,
                    Barcode = ci.Barcode,
                    ProductName = ci.ProductName,
                    Quantity = ci.Quantity,
                    UnitPrice = ci.UnitPrice,
                    MemberPrice = ci.MemberPrice > 0 ? ci.MemberPrice : null,
                    ActualPrice = ci.ActualPrice,
                    Subtotal = ci.Subtotal,
                    SaleUnit = ci.SaleUnit
                }).ToList()
            };

            await _orderRepo.CreateAsync(order);

            // 扣减库存
            foreach (var item in CartItems)
            {
                await _productRepo.UpdateStockAsync(item.ProductId, -item.Quantity);
            }

            // 累加会员积分
            if (CurrentMember != null && pointsEarned > 0)
            {
                await _memberRepo.UpdatePointsAsync(CurrentMember.Id, pointsEarned);
                CurrentMember.TotalPoints += pointsEarned;
                await _memberRepo.AddOrUpdateAsync(CurrentMember);
            }

            // 尝试打印小票
            try
            {
                if (_printer.IsConnected)
                {
                    _printer.PrintReceipt(order, settings);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"小票打印失败: {ex.Message}", "打印提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 清空购物车
            ClearCart();
        }

        private void RecalculateTotals()
        {
            SubTotal = CartItems.Sum(x => x.Quantity * x.UnitPrice);
            PayableAmount = CartItems.Sum(x => x.Subtotal);
            DiscountAmount = SubTotal - PayableAmount;
        }
    }
}
