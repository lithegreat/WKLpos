using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Hardware;
using WanKePos.WinUI.Messages;

namespace WanKePos.WinUI.ViewModels
{
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
        [NotifyPropertyChangedFor(nameof(MemberPanelVisibility))]
        private Member? _currentMember;

        public bool IsMemberMode => CurrentMember != null;
        public Microsoft.UI.Xaml.Visibility MemberPanelVisibility => IsMemberMode ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

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

        public Func<decimal, string, Task<(bool isConfirmed, decimal paidAmount)>>? RequestCashCheckoutDialog { get; set; }
        public Func<string, string, Task<bool>>? RequestConfirmDialog { get; set; }
        public Action<string, string>? ShowMessage { get; set; }

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

            // 监听商品变更消息，实现跨页面毫秒级响应式同步
            WeakReferenceMessenger.Default.Register<ProductsChangedMessage>(this, async (r, m) =>
            {
                if (m.DeletedProductId.HasValue)
                {
                    var toRemove = DisplayProducts.FirstOrDefault(p => p.Id == m.DeletedProductId.Value);
                    if (toRemove != null)
                    {
                        DisplayProducts.Remove(toRemove);
                    }
                    var cartToRemove = CartItems.FirstOrDefault(c => c.ProductId == m.DeletedProductId.Value);
                    if (cartToRemove != null)
                    {
                        CartItems.Remove(cartToRemove);
                        RecalculateTotals();
                    }
                }

                if (_isInitialized)
                {
                    await LoadCategoriesAsync();
                    await LoadProductsAsync();
                }
                else
                {
                    _needsRefresh = true;
                }
            });

            // 监听会员变更消息 (注销/删除会员时自动清空收银台关联)
            WeakReferenceMessenger.Default.Register<MembersChangedMessage>(this, (r, m) =>
            {
                if (m.DeletedMemberId.HasValue && CurrentMember?.Id == m.DeletedMemberId.Value)
                {
                    ClearMember();
                }
            });
        }

        private bool _isInitialized;
        private bool _needsRefresh;

        [RelayCommand]
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                if (_needsRefresh)
                {
                    _needsRefresh = false;
                    await LoadCategoriesAsync();
                    await LoadProductsAsync();
                }
                return;
            }
            _isInitialized = true;
            await LoadCategoriesAsync();
            await LoadProductsAsync();
        }

        public async Task LoadCategoriesAsync()
        {
            var categories = await _productRepo.GetCategoriesAsync();
            Categories.Clear();
            Categories.Add(CategoryConstants.All);
            foreach (var cat in categories)
                Categories.Add(cat);
            SelectedCategory = CategoryConstants.All;
        }

        public async Task LoadProductsAsync()
        {
            var products = string.IsNullOrWhiteSpace(SelectedCategory) || SelectedCategory == CategoryConstants.All
                ? await _productRepo.GetAllAsync()
                : await _productRepo.GetByCategoryAsync(SelectedCategory);

            DisplayProducts.Clear();
            foreach (var p in products.Where(p => p.Stock > 0))
                DisplayProducts.Add(p);
        }

        [RelayCommand]
        public async Task SearchOrScanAsync()
        {
            if (string.IsNullOrWhiteSpace(BarcodeInput)) return;

            var product = await _productRepo.GetByBarcodeAsync(BarcodeInput.Trim());
            if (product != null)
            {
                AddToCart(product);
            }
            else
            {
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
                    ShowMessage?.Invoke("提示", $"未找到商品: {BarcodeInput}");
                }
            }
            BarcodeInput = string.Empty;
        }

        [RelayCommand]
        public void AddToCart(Product p)
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
        public void RemoveFromCart(CartItem item)
        {
            CartItems.Remove(item);
            RecalculateTotals();
        }

        [RelayCommand]
        public void IncreaseQuantity(CartItem item)
        {
            item.Quantity++;
            RecalculateTotals();
        }

        [RelayCommand]
        public void DecreaseQuantity(CartItem item)
        {
            if (item.Quantity > 1)
                item.Quantity--;
            else
                CartItems.Remove(item);
            RecalculateTotals();
        }

        public async Task<List<Member>> SuggestMembersAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<Member>();
            var list = await _memberRepo.SearchAsync(query.Trim());
            return list.Where(m => m.Status == MemberStatusConstants.Normal).Take(10).ToList();
        }

        public void SelectMember(Member member)
        {
            if (member == null) return;
            CurrentMember = member;
            MemberPhoneInput = member.Phone ?? string.Empty;
            foreach (var item in CartItems)
            {
                if (item.MemberPrice > 0)
                    item.ActualPrice = item.MemberPrice;
            }
            RecalculateTotals();
        }

        [RelayCommand]
        public async Task SearchMemberAsync()
        {
            if (string.IsNullOrWhiteSpace(MemberPhoneInput)) return;

            var keyword = MemberPhoneInput.Trim();
            var member = await _memberRepo.GetByPhoneAsync(keyword);
            if (member == null)
            {
                var matches = await _memberRepo.SearchAsync(keyword);
                member = matches.FirstOrDefault(m => m.Status == MemberStatusConstants.Normal);
            }

            if (member != null && member.Status == MemberStatusConstants.Normal)
            {
                SelectMember(member);
            }
            else
            {
                ShowMessage?.Invoke("提示", member?.Status == MemberStatusConstants.Disabled ? "该会员已被禁用" : "未找到该会员");
            }
        }

        [RelayCommand]
        public void ClearMember()
        {
            CurrentMember = null;
            MemberPhoneInput = string.Empty;
            foreach (var item in CartItems)
                item.ActualPrice = item.UnitPrice;
            RecalculateTotals();
        }

        [RelayCommand]
        public void ClearCart()
        {
            CartItems.Clear();
            RecalculateTotals();
        }

        [RelayCommand]
        public async Task SelectCategoryAsync(string category)
        {
            SelectedCategory = category;
            await LoadProductsAsync();
        }

        [RelayCommand]
        public async Task CheckoutCashAsync()
        {
            if (!CartItems.Any()) return;

            if (RequestCashCheckoutDialog != null)
            {
                var (isConfirmed, paidAmount) = await RequestCashCheckoutDialog.Invoke(PayableAmount, "现金支付");
                if (isConfirmed)
                {
                    var changeAmount = paidAmount - PayableAmount;
                    await ProcessCheckoutAsync(PaymentMethod.Cash, paidAmount, changeAmount);
                    ShowMessage?.Invoke("✅ 交易完成", $"结算成功！\n实收: ¥{paidAmount:F2}\n找零: ¥{changeAmount:F2}");
                }
            }
        }

        [RelayCommand]
        public async Task CheckoutBalanceAsync()
        {
            if (!CartItems.Any() || !IsMemberMode) return;

            if (CurrentMember!.Balance < PayableAmount)
            {
                ShowMessage?.Invoke("提示", $"会员余额不足！\n当前余额: ¥{CurrentMember.Balance:F2}\n应付: ¥{PayableAmount:F2}");
                return;
            }

            bool confirmed = false;
            if (RequestConfirmDialog != null)
            {
                confirmed = await RequestConfirmDialog.Invoke("余额支付确认",
                    $"确认使用会员余额支付？\n会员: {CurrentMember.Name}\n余额: ¥{CurrentMember.Balance:F2}\n应付: ¥{PayableAmount:F2}");
            }

            if (confirmed)
            {
                await ProcessCheckoutAsync(PaymentMethod.MemberBalance, PayableAmount, 0);

                await _memberRepo.UpdateBalanceAsync(CurrentMember.Id, -PayableAmount);
                CurrentMember.Balance -= PayableAmount;

                WeakReferenceMessenger.Default.Send(new MembersChangedMessage());

                ShowMessage?.Invoke("✅ 交易完成", $"余额支付成功！\n剩余余额: ¥{CurrentMember.Balance:F2}");
            }
        }

        private async Task ProcessCheckoutAsync(PaymentMethod paymentMethod, decimal paidAmount, decimal changeAmount)
        {
            var settings = await _settingsRepo.GetSettingsAsync();

            var pointsRate = settings.PointsPerYuan > 0 ? settings.PointsPerYuan : 1m;
            decimal pointsEarned = Math.Floor(PayableAmount / pointsRate);

            // 获取商品当前进货价用于快照
            var productIds = CartItems.Select(ci => ci.ProductId).ToList();
            var costPriceMap = new Dictionary<int, decimal>();
            foreach (var ci in CartItems)
            {
                var product = await _productRepo.GetByBarcodeAsync(ci.Barcode);
                if (product != null)
                    costPriceMap[ci.ProductId] = product.CostPrice;
            }

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
                    CostPrice = costPriceMap.GetValueOrDefault(ci.ProductId),
                    ActualPrice = ci.ActualPrice,
                    Subtotal = ci.Subtotal,
                    SaleUnit = ci.SaleUnit
                }).ToList()
            };

            await _orderRepo.CreateAsync(order);
            WeakReferenceMessenger.Default.Send(new OrdersChangedMessage(order.Id));

            // 批量扣减库存并立即刷新收银台与商品管理页
            var stockChanges = CartItems.ToDictionary(ci => ci.ProductId, ci => -ci.Quantity);
            await _productRepo.BatchUpdateStockAsync(stockChanges);
            await LoadProductsAsync();
            WeakReferenceMessenger.Default.Send(new ProductsChangedMessage());

            // 记录会员累计消费与积分变动并广播
            if (CurrentMember != null)
            {
                await _memberRepo.RecordConsumptionAsync(CurrentMember.Id, PayableAmount, pointsEarned);
                CurrentMember.TotalSpent += PayableAmount;
                CurrentMember.TotalPoints += pointsEarned;
                WeakReferenceMessenger.Default.Send(new MembersChangedMessage());
            }

            try
            {
                if (_printer.IsConnected)
                {
                    _printer.PrintReceipt(order, settings);
                }
            }
            catch (Exception ex)
            {
                ShowMessage?.Invoke("打印提示", $"小票打印失败: {ex.Message}");
            }

            ClearCart();
            ClearMember();
        }

        private void RecalculateTotals()
        {
            SubTotal = CartItems.Sum(x => x.Quantity * x.UnitPrice);
            PayableAmount = CartItems.Sum(x => x.Subtotal);
            DiscountAmount = SubTotal - PayableAmount;
        }
    }
}
