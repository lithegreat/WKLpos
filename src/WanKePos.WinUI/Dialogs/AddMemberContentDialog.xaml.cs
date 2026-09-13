using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class AddMemberContentDialog : ContentDialog
{
    private readonly Member? _editingMember;
    public Member? CreatedMember { get; private set; }
    public bool IsEditMode => _editingMember != null;

    public AddMemberContentDialog(Member? editingMember = null)
    {
        this.InitializeComponent();
        _editingMember = editingMember;

        if (IsEditMode && _editingMember != null)
        {
            this.Title = "修改会员档案";
            this.PrimaryButtonText = "保存修改";

            PhoneTextBox.Text = _editingMember.Phone ?? string.Empty;
            NameTextBox.Text = _editingMember.Name ?? string.Empty;
            MemberNoTextBox.Text = _editingMember.MemberNo ?? string.Empty;

            // 性别
            SelectComboBoxItem(GenderComboBox, _editingMember.Gender ?? "保密");

            // 生日
            if (_editingMember.Birthday.HasValue)
            {
                BirthdayPicker.SelectedDate = new DateTimeOffset(_editingMember.Birthday.Value);
            }

            BalanceTextBox.Text = _editingMember.Balance.ToString("F2");
            PointsTextBox.Text = _editingMember.TotalPoints.ToString("0");

            // 会员身份
            SelectComboBoxItem(IdentityComboBox, _editingMember.Identity ?? "普通会员");

            // 状态
            SelectComboBoxItem(StatusComboBox, string.IsNullOrEmpty(_editingMember.Status) ? "正常" : _editingMember.Status);

            GuideNameTextBox.Text = _editingMember.GuideName ?? string.Empty;
            AddressTextBox.Text = _editingMember.Address ?? string.Empty;
        }
        else
        {
            MemberNoTextBox.Text = $"HY{DateTime.Now:yyMMdd}{Random.Shared.Next(1000, 9999)}";
        }

        this.Loaded += (s, e) =>
        {
            PhoneTextBox.Focus(FocusState.Programmatic);
        };
    }

    private static void SelectComboBoxItem(ComboBox comboBox, string text)
    {
        for (int i = 0; i < comboBox.Items.Count; i++)
        {
            if (comboBox.Items[i] is ComboBoxItem item && string.Equals(item.Content?.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedIndex = i;
                return;
            }
        }
        if (comboBox.Items.Count > 0) comboBox.SelectedIndex = 0;
    }

    private void GenerateMemberNoButton_Click(object sender, RoutedEventArgs e)
    {
        MemberNoTextBox.Text = $"HY{DateTime.Now:yyMMdd}{Random.Shared.Next(1000, 9999)}";
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var phone = PhoneTextBox.Text?.Trim();
        var name = NameTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(phone) || phone.Length < 7)
        {
            ShowError("请输入有效的会员手机号！");
            args.Cancel = true;
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            ShowError("会员姓名不能为空！");
            args.Cancel = true;
            return;
        }

        var memberNo = string.IsNullOrWhiteSpace(MemberNoTextBox.Text)
            ? $"HY{DateTime.Now:yyMMdd}{Random.Shared.Next(1000, 9999)}"
            : MemberNoTextBox.Text.Trim();

        decimal.TryParse(BalanceTextBox.Text?.Trim(), out var balance);
        decimal.TryParse(PointsTextBox.Text?.Trim(), out var points);

        var gender = (GenderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "保密";
        var identity = (IdentityComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "普通会员";
        var status = (StatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "正常";

        if (IsEditMode && _editingMember != null)
        {
            _editingMember.Phone = phone;
            _editingMember.Name = name;
            _editingMember.MemberNo = memberNo;
            _editingMember.Gender = gender;
            _editingMember.Birthday = BirthdayPicker.SelectedDate?.DateTime;
            _editingMember.Balance = balance;
            _editingMember.TotalPoints = points;
            _editingMember.Identity = identity;
            _editingMember.Status = status;
            _editingMember.GuideName = GuideNameTextBox.Text?.Trim();
            _editingMember.Address = AddressTextBox.Text?.Trim();
            _editingMember.LastModified = DateTime.Now;

            CreatedMember = _editingMember;
        }
        else
        {
            CreatedMember = new Member
            {
                Phone = phone,
                Name = name,
                MemberNo = memberNo,
                Gender = gender,
                Birthday = BirthdayPicker.SelectedDate?.DateTime,
                Balance = balance,
                TotalPoints = points,
                TotalSpent = 0,
                Identity = identity,
                Status = status,
                GuideName = GuideNameTextBox.Text?.Trim(),
                Address = AddressTextBox.Text?.Trim(),
                StoreName = "万客隆美发用品专卖",
                RegisterTime = DateTime.Now,
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now,
                SyncStatus = SyncStatus.Pending
            };
        }
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
