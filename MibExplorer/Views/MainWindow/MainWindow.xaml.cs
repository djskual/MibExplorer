using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MibExplorer.Models;
using MibExplorer.ViewModels;

namespace MibExplorer.Views.MainWindow;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Password = PasswordBox.Password;
    }

    private void RemoteTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is RemoteExplorerItem item)
            ViewModel.SelectedTreeNode = item;
    }

    private void CurrentFolderList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListView listView || listView.SelectedItem is not RemoteExplorerItem item)
            return;

        if (!item.IsDirectory)
            return;

        ViewModel.SelectedTreeNode = item;
    }
}
