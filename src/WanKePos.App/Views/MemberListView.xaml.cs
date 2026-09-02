using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WanKePos.App.Dialogs;
using WanKePos.App.ViewModels;
using WanKePos.Domain.Entities;

namespace WanKePos.App.Views;

public partial class MemberListView : Page
{
    public MemberListViewModel ViewModel { get; }

    public MemberListView(MemberListViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestAddMemberDialog = () =>
        {
            var dialog = new AddMemberDialog
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult(dialog.CreatedMember);
            }
            return Task.FromResult<Member?>(null);
        };

        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
        };
    }

    private void DeleteMemberButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is Member member)
        {
            ViewModel.DeleteMemberCommand.Execute(member);
        }
    }
}
