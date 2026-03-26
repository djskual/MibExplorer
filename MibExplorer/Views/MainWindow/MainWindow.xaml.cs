using MibExplorer.Models;
using MibExplorer.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MibExplorer.Views.MainWindow;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private bool _isSortingFromHeader;
    private string? _pendingSortKey;
    private const string HeaderName = "Name";
    private const string HeaderType = "Type";
    private const string HeaderSize = "Size";
    private const string HeaderModified = "Modified";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();

        Loaded += (_, _) => UpdateSortHeaderVisuals();

        CurrentFolderList.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(CurrentFolderHeader_PreviewMouseLeftButtonDown), true);
    }

    private static string NormalizeHeaderText(string? headerText)
    {
        if (string.IsNullOrWhiteSpace(headerText))
            return string.Empty;

        return headerText
            .Replace(" ▲", string.Empty)
            .Replace(" ▼", string.Empty)
            .Trim();
    }

    private void UpdateSortHeaderVisuals()
    {
        if (CurrentFolderList.View is not GridView gridView)
            return;

        var activeColumn = ViewModel.ActiveSortColumn?.Trim();
        var arrow = ViewModel.IsSortAscending ? " ▲" : " ▼";

        foreach (var column in gridView.Columns)
        {
            if (column.Header is not string headerText)
                continue;

            var baseHeader = NormalizeHeaderText(headerText);

            if (string.IsNullOrWhiteSpace(baseHeader))
                continue;

            column.Header = string.Equals(baseHeader, activeColumn, StringComparison.OrdinalIgnoreCase)
                ? baseHeader + arrow
                : baseHeader;
        }
    }

    private void CurrentFolderHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isSortingFromHeader)
            return;

        if (e.OriginalSource is not DependencyObject source)
            return;

        var header = FindVisualParent<GridViewColumnHeader>(source);
        if (header?.Column == null)
            return;

        string? sortKey = null;

        if (header.Column.Header is string headerText)
            sortKey = NormalizeHeaderText(headerText);
        else if (header.Column.Header is TextBlock tb)
            sortKey = NormalizeHeaderText(tb.Text);

        if (string.IsNullOrWhiteSpace(sortKey))
            return;

        // On bloque le comportement natif du header
        e.Handled = true;

        _pendingSortKey = sortKey;

        // On capture AVANT toute reconstruction
        FreezeCurrentFolderList();

        // On force l'overlay à être réellement visible tout de suite
        CurrentFolderListFreezeOverlay.UpdateLayout();

        _isSortingFromHeader = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            ExecutePendingHeaderSort();
        }), DispatcherPriority.Send);
    }

    private void FreezeCurrentFolderList()
    {
        CurrentFolderList.UpdateLayout();

        double width = CurrentFolderList.ActualWidth;
        double height = CurrentFolderList.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var rtb = new RenderTargetBitmap(
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            96,
            96,
            PixelFormats.Pbgra32);

        rtb.Render(CurrentFolderList);

        CurrentFolderListFreezeOverlay.Source = rtb;
        CurrentFolderListFreezeOverlay.Visibility = Visibility.Visible;
        CurrentFolderListFreezeOverlay.Opacity = 1.0;
        CurrentFolderListFreezeOverlay.UpdateLayout();
    }

    private async void UnfreezeCurrentFolderListDelayed()
    {
        await Task.Delay(25);

        CurrentFolderListFreezeOverlay.Source = null;
        CurrentFolderListFreezeOverlay.Visibility = Visibility.Collapsed;
        CurrentFolderListFreezeOverlay.Opacity = 1.0;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T typed)
                return typed;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null)
            return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
                return typedChild;

            var result = FindVisualChild<T>(child);
            if (result is not null)
                return result;
        }

        return null;
    }

    private void ExecutePendingHeaderSort()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_pendingSortKey))
                return;

            var scrollViewer = FindVisualChild<ScrollViewer>(CurrentFolderList);
            double horizontalOffset = scrollViewer?.HorizontalOffset ?? 0;
            double verticalOffset = scrollViewer?.VerticalOffset ?? 0;

            ViewModel.SortCurrentFolder(_pendingSortKey);
            UpdateSortHeaderVisuals();

            CurrentFolderList.UpdateLayout();

            var updatedScrollViewer = FindVisualChild<ScrollViewer>(CurrentFolderList);
            if (updatedScrollViewer is not null)
            {
                updatedScrollViewer.ScrollToHorizontalOffset(horizontalOffset);
                updatedScrollViewer.ScrollToVerticalOffset(verticalOffset);
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                UnfreezeCurrentFolderListDelayed();
                _pendingSortKey = null;
                _isSortingFromHeader = false;
            }), DispatcherPriority.Render);
        }
        catch
        {
            UnfreezeCurrentFolderListDelayed();
            _pendingSortKey = null;
            _isSortingFromHeader = false;
            throw;
        }
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

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }
}
