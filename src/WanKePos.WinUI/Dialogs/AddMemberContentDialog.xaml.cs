using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class AddMemberContentDialog : ContentDialog
{
    public Member? CreatedMember { get; private set; }

    public AddMemberContentDialog()
    {
        this.InitializeComponent();

        MemberNoTextBox.Text = $"HY{DateTime.Now:yyMMdd}{Random.Shared.Next(1000, 9999)}";

        this.Loaded += (s, e) =>
        {
            PhoneTextBox.Focus(FocusState.Programmatic);
        };
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
            Status = "正常",
            GuideName = GuideNameTextBox.Text?.Trim(),
            Address = AddressTextBox.Text?.Trim(),
            StoreName = "万客隆美发用品专卖",
            RegisterTime = DateTime.Now,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now,
            SyncStatus = SyncStatus.Pending
        };
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
