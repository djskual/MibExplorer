using MibExplorer.ViewModels;
using MibExplorer.Views.Dialogs;
using MibExplorer.Views.FileEditor;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Forms = System.Windows.Forms;

namespace MibExplorer.Views.Scripting;

public partial class ScriptRunnerWindow : Window
{
    private const double FineLogScrollPixelsPerDetent = 22.0;
    private const double ExecutionLogBottomTolerance = 4.0;

    private readonly Dictionary<string, FileEditorWindow> _openLocalEditors = new(StringComparer.OrdinalIgnoreCase);

    private ScrollViewer? _executionLogScrollViewer;
    private bool _followExecutionLog = true;
    private bool _executionLogAutoScrolling;

    public ScriptRunnerWindow()
    {
        InitializeComponent();

        Loaded += ScriptRunnerWindow_Loaded;
        ExecutionLogTextBox.PreviewMouseWheel += ExecutionLogTextBox_PreviewMouseWheel;
        ExecutionLogTextBox.TextChanged += ExecutionLogTextBox_TextChanged;
    }

    private void ScriptRunnerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _executionLogScrollViewer = FindVisualChild<ScrollViewer>(ExecutionLogTextBox);

        if (_executionLogScrollViewer is not null)
        {
            _executionLogScrollViewer.ScrollChanged += ExecutionLogScrollViewer_ScrollChanged;
            _followExecutionLog = IsExecutionLogNearBottom(_executionLogScrollViewer);
        }
    }

    private void BrowseScriptsFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ScriptRunnerViewModel vm)
            return;

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select scripts folder",
            ShowNewFolderButton = true
        };

        if (!string.IsNullOrWhiteSpace(vm.ScriptsFolderPath) &&
            Directory.Exists(vm.ScriptsFolderPath))
        {
            dialog.SelectedPath = vm.ScriptsFolderPath;
        }

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            vm.SetScriptsFolderPath(dialog.SelectedPath);
        }
    }

    private void OpenInEditor_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ScriptRunnerViewModel vm)
            return;

        if (vm.SelectedScript is null)
            return;

        string localPath = vm.SelectedScript.LocalPath;

        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
        {
            MessageBox.Show(
                this,
                "The selected script file was not found.",
                "Open in Editor",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        string fullPath = Path.GetFullPath(localPath);

        if (_openLocalEditors.TryGetValue(fullPath, out FileEditorWindow? existingWindow))
        {
            if (existingWindow.WindowState == WindowState.Minimized)
                existingWindow.WindowState = WindowState.Normal;

            existingWindow.Show();
            existingWindow.Activate();
            existingWindow.Focus();
            return;
        }

        bool isReadOnly = false;

        try
        {
            var attributes = File.GetAttributes(fullPath);
            isReadOnly = attributes.HasFlag(FileAttributes.ReadOnly);
        }
        catch
        {
        }

        var viewModel = new FileEditorViewModel(fullPath, isReadOnly);
        var window = new FileEditorWindow(viewModel);

        window.Closed += (_, _) =>
        {
            _openLocalEditors.Remove(fullPath);
        };

        _openLocalEditors[fullPath] = window;

        window.Show();
        window.Activate();
    }

    private void ScriptsListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenInEditor_Click(sender, e);
    }

    private void ExecutionLogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_followExecutionLog)
            return;

        ExecutionLogTextBox.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_executionLogScrollViewer is null)
                _executionLogScrollViewer = FindVisualChild<ScrollViewer>(ExecutionLogTextBox);

            if (_executionLogScrollViewer is null)
            {
                ExecutionLogTextBox.ScrollToEnd();
                return;
            }

            _executionLogAutoScrolling = true;
            ExecutionLogTextBox.ScrollToEnd();
            _executionLogAutoScrolling = false;
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void ExecutionLogScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_executionLogAutoScrolling)
            return;

        if (sender is not ScrollViewer scrollViewer)
            return;

        _followExecutionLog = IsExecutionLogNearBottom(scrollViewer);
    }

    private void ExecutionLogTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyFineVerticalScroll(ExecutionLogTextBox, e);
    }

    private void ApplyFineVerticalScroll(DependencyObject source, MouseWheelEventArgs e)
    {
        ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(source);
        if (scrollViewer is null)
            return;

        double deltaSteps = e.Delta / 120.0;
        double targetOffset = scrollViewer.VerticalOffset - (deltaSteps * FineLogScrollPixelsPerDetent);

        if (targetOffset < 0)
            targetOffset = 0;
        else if (targetOffset > scrollViewer.ScrollableHeight)
            targetOffset = scrollViewer.ScrollableHeight;

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
    }

    private static bool IsExecutionLogNearBottom(ScrollViewer scrollViewer)
    {
        return scrollViewer.VerticalOffset >=
               scrollViewer.ScrollableHeight - ExecutionLogBottomTolerance;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null)
            return null;

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
}