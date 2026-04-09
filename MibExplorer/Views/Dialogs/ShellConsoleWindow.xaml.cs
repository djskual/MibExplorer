using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Documents;
using MibExplorer.ViewModels;

namespace MibExplorer.Views.Dialogs;

public partial class ShellConsoleWindow : Window
{
    private readonly ShellConsoleViewModel _viewModel;

    public ShellConsoleWindow(ShellConsoleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        _viewModel.AttachDocument(OutputRichTextBox.Document);

        Loaded += ShellConsoleWindow_Loaded;
    }

    private async void ShellConsoleWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ShellConsoleWindow_Loaded;

        Activated += ShellConsoleWindow_Activated;

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
        if (e.PropertyName == nameof(ShellConsoleViewModel.OutputText))
        {
            Dispatcher.BeginInvoke(new Action(() => OutputRichTextBox.ScrollToEnd()));
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Activated -= ShellConsoleWindow_Activated;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}