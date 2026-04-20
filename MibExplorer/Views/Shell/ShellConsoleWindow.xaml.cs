using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using MibExplorer.ViewModels;

namespace MibExplorer.Views.Dialogs;

public partial class ShellConsoleWindow : Window
{
    private const double OutputBottomTolerance = 4.0;

    private readonly ShellConsoleViewModel _viewModel;
    private ScrollViewer? _outputScrollViewer;
    private bool _followOutput = true;
    private bool _outputAutoScrolling;

    public ShellConsoleWindow(ShellConsoleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        _viewModel.AttachDocument(OutputRichTextBox.Document);

        Loaded += ShellConsoleWindow_Loaded;
        OutputRichTextBox.PreviewMouseWheel += OutputRichTextBox_PreviewMouseWheel;
    }

    private async void ShellConsoleWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ShellConsoleWindow_Loaded;

        Activated += ShellConsoleWindow_Activated;

        _outputScrollViewer = FindVisualChild<ScrollViewer>(OutputRichTextBox);
        if (_outputScrollViewer is not null)
        {
            _outputScrollViewer.ScrollChanged += OutputScrollViewer_ScrollChanged;
            _followOutput = IsNearBottom(_outputScrollViewer);
        }

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        await _viewModel.InitializeAsync();

        CommandTextBox.Focus();
        OutputRichTextBox.ScrollToEnd();
    }

    private void ShellConsoleWindow_Activated(object? sender, EventArgs e)
    {
        CommandTextBox.Focus();
        CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
    }

    private void CommandTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            e.Handled = true;
            _viewModel.BrowseHistoryUp();
            CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            return;
        }

        if (e.Key == Key.Down)
        {
            e.Handled = true;
            _viewModel.BrowseHistoryDown();
            CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
            return;
        }
    }

    private async void CommandTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _viewModel.ClearCommand.Execute(null);
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;

            if (_viewModel.SendCommand.CanExecute(null))
                await _viewModel.SendCurrentCommandAsync();

            CommandTextBox.CaretIndex = CommandTextBox.Text.Length;
        }
    }

    private void CommandContextClear_Click(object sender, RoutedEventArgs e)
    {
        CommandTextBox.Clear();
        CommandTextBox.Focus();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellConsoleViewModel.OutputText))
            return;

        if (!_followOutput)
            return;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_outputScrollViewer is null)
                _outputScrollViewer = FindVisualChild<ScrollViewer>(OutputRichTextBox);

            _outputAutoScrolling = true;
            OutputRichTextBox.ScrollToEnd();
            _outputAutoScrolling = false;
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OutputScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_outputAutoScrolling)
            return;

        if (sender is not ScrollViewer scrollViewer)
            return;

        _followOutput = IsNearBottom(scrollViewer);
    }

    private void OutputRichTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ScrollViewer? scrollViewer = _outputScrollViewer ?? FindVisualChild<ScrollViewer>(OutputRichTextBox);
        if (scrollViewer is null)
            return;

        double deltaSteps = e.Delta / 120.0;
        double targetOffset = scrollViewer.VerticalOffset - (deltaSteps * 22.0);

        if (targetOffset < 0)
            targetOffset = 0;
        else if (targetOffset > scrollViewer.ScrollableHeight)
            targetOffset = scrollViewer.ScrollableHeight;

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private static bool IsNearBottom(ScrollViewer scrollViewer)
    {
        return scrollViewer.VerticalOffset >=
               scrollViewer.ScrollableHeight - OutputBottomTolerance;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
                return typedChild;

            T? descendant = FindVisualChild<T>(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }

    protected override void OnClosed(EventArgs e)
    {
        Activated -= ShellConsoleWindow_Activated;
        OutputRichTextBox.PreviewMouseWheel -= OutputRichTextBox_PreviewMouseWheel;

        if (_outputScrollViewer is not null)
        {
            _outputScrollViewer.ScrollChanged -= OutputScrollViewer_ScrollChanged;
        }

        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}