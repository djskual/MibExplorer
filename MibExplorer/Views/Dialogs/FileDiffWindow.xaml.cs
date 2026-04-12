using MibExplorer.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MibExplorer.Views.Dialogs;

public partial class FileDiffWindow : Window
{
    private readonly FileDiffViewModel _viewModel;

    private ScrollViewer? _originalScrollViewer;
    private ScrollViewer? _currentScrollViewer;

    private bool _isSyncingScroll;
    private bool _isSyncingSelection;

    public FileDiffWindow(FileDiffViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += FileDiffWindow_Loaded;
    }

    private void FileDiffWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= FileDiffWindow_Loaded;

        _originalScrollViewer = FindVisualChild<ScrollViewer>(OriginalListBox);
        _currentScrollViewer = FindVisualChild<ScrollViewer>(CurrentListBox);

        if (_originalScrollViewer is not null)
            _originalScrollViewer.ScrollChanged += OriginalScrollViewer_ScrollChanged;

        if (_currentScrollViewer is not null)
            _currentScrollViewer.ScrollChanged += CurrentScrollViewer_ScrollChanged;

        if (_viewModel.ChangedLineIndices.Count > 0)
            NavigateToChangedLine(0);
    }

    private void OriginalScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        SyncScrollOffsets(_originalScrollViewer, _currentScrollViewer);
    }

    private void CurrentScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        SyncScrollOffsets(_currentScrollViewer, _originalScrollViewer);
    }

    private void SyncScrollOffsets(ScrollViewer? source, ScrollViewer? target)
    {
        if (_isSyncingScroll || source is null || target is null)
            return;

        try
        {
            _isSyncingScroll = true;

            if (!DoubleUtil.AreClose(target.VerticalOffset, source.VerticalOffset))
                target.ScrollToVerticalOffset(source.VerticalOffset);

            if (!DoubleUtil.AreClose(target.HorizontalOffset, source.HorizontalOffset))
                target.ScrollToHorizontalOffset(source.HorizontalOffset);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OriginalListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection)
            return;

        try
        {
            _isSyncingSelection = true;
            CurrentListBox.SelectedIndex = OriginalListBox.SelectedIndex;
            UpdateCurrentDiffIndexFromSelection(OriginalListBox.SelectedIndex);
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void CurrentListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingSelection)
            return;

        try
        {
            _isSyncingSelection = true;
            OriginalListBox.SelectedIndex = CurrentListBox.SelectedIndex;
            UpdateCurrentDiffIndexFromSelection(CurrentListBox.SelectedIndex);
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void UpdateCurrentDiffIndexFromSelection(int selectedIndex)
    {
        int changedLineListIndex = -1;

        for (int i = 0; i < _viewModel.ChangedLineIndices.Count; i++)
        {
            if (_viewModel.ChangedLineIndices[i] == selectedIndex)
            {
                changedLineListIndex = i;
                break;
            }
        }

        _viewModel.UpdateCurrentDiffPosition(changedLineListIndex);
    }

    private void PreviousDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ChangedLineIndices.Count == 0)
            return;

        int currentIndex = OriginalListBox.SelectedIndex;
        int targetChangedIndex = _viewModel.ChangedLineIndices.Count - 1;

        for (int i = _viewModel.ChangedLineIndices.Count - 1; i >= 0; i--)
        {
            if (_viewModel.ChangedLineIndices[i] < currentIndex)
            {
                targetChangedIndex = i;
                break;
            }
        }

        NavigateToChangedLine(targetChangedIndex);
    }

    private void NextDiff_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ChangedLineIndices.Count == 0)
            return;

        int currentIndex = OriginalListBox.SelectedIndex;
        int targetChangedIndex = 0;

        for (int i = 0; i < _viewModel.ChangedLineIndices.Count; i++)
        {
            if (_viewModel.ChangedLineIndices[i] > currentIndex)
            {
                targetChangedIndex = i;
                break;
            }
        }

        NavigateToChangedLine(targetChangedIndex);
    }

    private void NavigateToChangedLine(int changedLineListIndex)
    {
        if (_viewModel.ChangedLineIndices.Count == 0)
            return;

        if (changedLineListIndex < 0)
            changedLineListIndex = 0;

        if (changedLineListIndex >= _viewModel.ChangedLineIndices.Count)
            changedLineListIndex = _viewModel.ChangedLineIndices.Count - 1;

        int itemIndex = _viewModel.ChangedLineIndices[changedLineListIndex];

        try
        {
            _isSyncingSelection = true;

            OriginalListBox.SelectedIndex = itemIndex;
            CurrentListBox.SelectedIndex = itemIndex;

            if (itemIndex >= 0 && itemIndex < OriginalListBox.Items.Count)
                OriginalListBox.ScrollIntoView(OriginalListBox.Items[itemIndex]);

            if (itemIndex >= 0 && itemIndex < CurrentListBox.Items.Count)
                CurrentListBox.ScrollIntoView(CurrentListBox.Items[itemIndex]);
        }
        finally
        {
            _isSyncingSelection = false;
        }

        _viewModel.UpdateCurrentDiffPosition(changedLineListIndex);
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
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

    private static class DoubleUtil
    {
        public static bool AreClose(double left, double right)
        {
            return Math.Abs(left - right) < 0.5;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}