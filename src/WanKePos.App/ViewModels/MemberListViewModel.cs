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

public partial class MemberListViewModel : ObservableObject
{
    private readonly IMemberRepository _memberRepository;
    private readonly ExcelImporter _excelImporter;

    public ObservableCollection<Member> Members { get; } = new();

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    public Func<Task<Member?>>? RequestAddMemberDialog { get; set; }

    public MemberListViewModel(IMemberRepository memberRepository, ExcelImporter excelImporter)
    {
        _memberRepository = memberRepository;
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
        var members = await _memberRepository.GetAllAsync();
        Members.Clear();
        foreach (var m in members) Members.Add(m);
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        var results = await _memberRepository.SearchAsync(SearchKeyword);
        Members.Clear();
        foreach (var m in results) Members.Add(m);
    }

    [RelayCommand]
    public async Task AddMemberAsync()
    {
        if (RequestAddMemberDialog != null)
        {
            var member = await RequestAddMemberDialog.Invoke();
            if (member != null)
            {
                try
                {
                    var existing = await _memberRepository.GetByPhoneAsync(member.Phone);
                    if (existing != null)
                    {
                        MessageBox.Show($"手机号【{member.Phone}】已注册为会员【{existing.Name}】！", "开卡失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    await _memberRepository.AddOrUpdateAsync(member);
                    MessageBox.Show($"新会员【{member.Name}】(手机: {member.Phone}) 已成功开通！", "开卡成功", MessageBoxButton.OK, MessageBoxImage.Information);
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
    public async Task DeleteMemberAsync(Member member)
    {
        if (member == null) return;

        var result = MessageBox.Show($"确认注销并删除会员【{member.Name}】(手机: {member.Phone}, 余额: ¥{member.Balance:F2}) 吗？\n注销后该会员数据将永久清除。", "确认注销会员", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            await _memberRepository.DeleteAsync(member.Id);
            MessageBox.Show($"会员【{member.Name}】已成功注销。", "注销成功", MessageBoxButton.OK, MessageBoxImage.Information);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"注销失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ImportFromExcelAsync()
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
                MessageBox.Show($"成功导入 {members.Count} 个会员。", "导入成功");
                await ReloadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
