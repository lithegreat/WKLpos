using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Import;

namespace WanKePos.WinUI.ViewModels
{
    public partial class MemberListViewModel : ObservableObject
    {
        private readonly IMemberRepository _memberRepository;
        private readonly ExcelImporter _excelImporter;

        public ObservableCollection<Member> Members { get; } = new();

        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        public Func<Task<string?>>? RequestOpenFileDialog { get; set; }
        public Action<string, string>? ShowMessage { get; set; }

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
                    }
                    catch (Exception ex)
                    {
                        ShowMessage?.Invoke("导入失败", $"错误: {ex.Message}");
                    }
                }
            }
        }
    }
}
