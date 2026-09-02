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

namespace WanKePos.App.ViewModels
{
    public partial class MemberListViewModel : ObservableObject
    {
        private readonly IMemberRepository _memberRepository;
        private readonly ExcelImporter _excelImporter;

        public ObservableCollection<Member> Members { get; } = new();

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
        public async Task SearchAsync(string keyword)
        {
            var results = await _memberRepository.SearchAsync(keyword);
            Members.Clear();
            foreach (var m in results) Members.Add(m);
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
                    await InitializeAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
