using System;
using System.Windows;
using System.Windows.Controls;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using Wpf.Ui.Controls;

namespace WanKePos.App.Dialogs;

public partial class AddMemberDialog : FluentWindow
{
    public Member? CreatedMember { get; private set; }

    public AddMemberDialog()
    {
        InitializeComponent();

        MemberNoTextBox.Text = $"HY{DateTime.Now:yyMMdd}{Random.Shared.Next(1000, 9999)}";

        Loaded += (s, e) =>
        {
            PhoneTextBox.Focus();
        };
    }

    private void GenerateMemberNoButton_Click(object sender, RoutedEventArgs e)
    {
        MemberNoTextBox.Text = $"HY{DateTime.Now:yyMMdd}{Random.Shared.Next(1000, 9999)}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var phone = PhoneTextBox.Text?.Trim();
        var name = NameTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(phone) || phone.Length < 7)
        {
            ShowError("请输入有效的会员手机号！");
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            ShowError("会员姓名不能为空！");
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
            Birthday = BirthdayPicker.SelectedDate,
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

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
