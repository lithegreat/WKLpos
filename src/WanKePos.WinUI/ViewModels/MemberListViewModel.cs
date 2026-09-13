using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Import;
using WanKePos.WinUI.Messages;

namespace WanKePos.WinUI.ViewModels;

public partial class MemberListViewModel : ObservableObject
{
    private readonly IMemberRepository _memberRepository;
    private readonly ExcelImporter _excelImporter;

    public ObservableCollection<Member> Members { get; } = new();

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    public Func<Task<string?>>? RequestOpenFileDialog { get; set; }
    public Func<Task<Member?>>? RequestAddMemberDialog { get; set; }
    public Func<string, string, Task<bool>>? RequestConfirm { get; set; }
    public Action<string, string>? ShowMessage { get; set; }

    public MemberListViewModel(IMemberRepository memberRepository, ExcelImporter excelImporter)
    {
        _memberRepository = memberRepository;
        _excelImporter = excelImporter;

        WeakReferenceMessenger.Default.Register<MembersChangedMessage>(this, async (r, m) =>
        {
            if (_isInitialized)
            {
                await ReloadAsync();
            }
            else
            {
                _needsRefresh = true;
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
                await ReloadAsync();
            }
            return;
        }
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
                    // 检查手机号是否重复
                    var existing = await _memberRepository.GetByPhoneAsync(member.Phone);
                    if (existing != null)
                    {
                        ShowMessage?.Invoke("开卡失败", $"手机号【{member.Phone}】已注册为会员【{existing.Name}】！");
                        return;
                    }

                    await _memberRepository.AddOrUpdateAsync(member);
                    ShowMessage?.Invoke("开卡成功", $"新会员【{member.Name}】(手机: {member.Phone}) 已成功开通！");
                    await ReloadAsync();
                    WeakReferenceMessenger.Default.Send(new MembersChangedMessage());
                }
                catch (Exception ex)
                {
                    ShowMessage?.Invoke("保存失败", $"错误: {ex.Message}");
                }
            }
        }
    }

    [RelayCommand]
    public async Task DeleteMemberAsync(Member member)
    {
        if (member == null) return;

        if (RequestConfirm != null)
        {
            var confirm = await RequestConfirm.Invoke("确认注销会员", $"确认注销并删除会员【{member.Name}】(手机: {member.Phone}, 余额: ¥{member.Balance:F2}) 吗？\n注销后该会员数据将永久清除。");
            if (!confirm) return;
        }

        try
        {
            await _memberRepository.DeleteAsync(member.Id);
            ShowMessage?.Invoke("注销成功", $"会员【{member.Name}】已成功注销。");
            await ReloadAsync();
            WeakReferenceMessenger.Default.Send(new MembersChangedMessage(member.Id));
        }
        catch (Exception ex)
        {
            ShowMessage?.Invoke("注销失败", $"错误: {ex.Message}");
        }
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
                    var members = await _excelImporter.ImportMembersAsync(filePath);
                    await _memberRepository.ImportFromListAsync(members);
                    ShowMessage?.Invoke("导入成功", $"成功导入 {members.Count} 个会员。");
                    await ReloadAsync();
                    WeakReferenceMessenger.Default.Send(new MembersChangedMessage());
                }
                catch (Exception ex)
                {
                    ShowMessage?.Invoke("导入失败", $"错误: {ex.Message}");
                }
            }
        }
    }
}
