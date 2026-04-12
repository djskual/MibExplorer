using MibExplorer.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace MibExplorer.Views.Dialogs;

public partial class FileEditorWindow : Window
{
    private bool _hasStartedLoading;
    private bool _isHandlingCloseProtection;
    private bool _isUpdatingEditorFromViewModel;

    public FileEditorWindow(FileEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ConfigureEditor();

        ContentRendered += FileEditorWindow_ContentRendered;
        Closing += FileEditorWindow_Closing;
    }

    public FileEditorViewModel ViewModel { get; }

    public string RemotePath => ViewModel.RemotePath;

    private void ConfigureEditor()
    {
        EditorTextBox.Options.ConvertTabsToSpaces = false;
        EditorTextBox.Options.IndentationSize = 4;

        EditorTextBox.TextChanged += EditorTextBox_TextChanged;
    }

    private void EditorTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingEditorFromViewModel)
            return;

        if (!string.Equals(ViewModel.EditorText, EditorTextBox.Text, StringComparison.Ordinal))
            ViewModel.EditorText = EditorTextBox.Text;
    }

    private void UpdateEditorTextFromViewModel()
    {
        try
        {
            _isUpdatingEditorFromViewModel = true;

            if (!string.Equals(EditorTextBox.Text, ViewModel.EditorText, StringComparison.Ordinal))
                EditorTextBox.Text = ViewModel.EditorText ?? string.Empty;
        }
        finally
        {
            _isUpdatingEditorFromViewModel = false;
        }
    }

    private void FocusEditorAtStart()
    {
        EditorTextBox.Focus();
        EditorTextBox.TextArea.Caret.Offset = 0;
        EditorTextBox.ScrollToLine(1);
    }

    private async void FileEditorWindow_ContentRendered(object? sender, EventArgs e)
    {
        if (_hasStartedLoading)
            return;

        _hasStartedLoading = true;

        await ViewModel.LoadAsync();

        UpdateEditorTextFromViewModel();
        FocusEditorAtStart();
    }

    private void Diff_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanShowDiff)
            return;

        var diffViewModel = new FileDiffViewModel(
            ViewModel.RemotePath,
            ViewModel.OriginalText,
            ViewModel.EditorText);

        var diffWindow = new FileDiffWindow(diffViewModel);

        double left = Left + Math.Max(0, (ActualWidth - diffWindow.Width) / 2);
        double top = Top + Math.Max(0, (ActualHeight - diffWindow.Height) / 2);

        diffWindow.Left = left;
        diffWindow.Top = top;

        diffWindow.Show();
        diffWindow.Activate();
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanReload)
            return;

        if (ViewModel.IsDirty && !ViewModel.IsReadOnly)
        {
            var result = AppMessageBox.Show(
                this,
                "Reloading this file will discard unsaved changes.\n\nContinue?",
                "Remote File Editor",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;
        }

        await ViewModel.ReloadAsync();

        UpdateEditorTextFromViewModel();
        FocusEditorAtStart();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void FileEditorWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isHandlingCloseProtection)
            return;

        if (!ViewModel.IsDirty || ViewModel.IsReadOnly)
            return;

        e.Cancel = true;

        var result = AppMessageBox.Show(
            this,
            "This file has unsaved changes.\n\nDo you want to save them before closing?",
            "Remote File Editor",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
            return;

        if (result == MessageBoxResult.No)
        {
            _isHandlingCloseProtection = true;

            await Dispatcher.BeginInvoke(new Action(() => Close()));
            return;
        }

        await ViewModel.SaveAsync();

        if (ViewModel.IsDirty)
            return;

        _isHandlingCloseProtection = true;

        await Dispatcher.BeginInvoke(new Action(() => Close()));
    }
}