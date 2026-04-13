using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using MibExplorer.ViewModels;
using MibExplorer.Views.FileEditor;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MibExplorer.Views.Dialogs;

public partial class FileEditorWindow : Window
{
    private const double FineEditorScrollPixelsPerDetent = 26.0; 

    private readonly FileEditorInlineDiffLineBackgroundRenderer _inlineDiffLineBackgroundRenderer = new();
    private readonly FileEditorInlineDiffSegmentTransformer _inlineDiffSegmentTransformer = new();
    private readonly FileEditorInlineDiffMarkerMargin _inlineDiffMarkerMargin = new();

    private List<int> _diffLineNumbers = [];
    private int _currentDiffIndex = -1;
    private bool _caretIsExactlyOnDiffLine;

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

        EditorTextBox.TextArea.LeftMargins.Add(_inlineDiffMarkerMargin);
        EditorTextBox.TextArea.TextView.BackgroundRenderers.Add(_inlineDiffLineBackgroundRenderer);
        EditorTextBox.TextArea.TextView.LineTransformers.Add(_inlineDiffSegmentTransformer);

        EditorTextBox.TextChanged += EditorTextBox_TextChanged;
        EditorTextBox.PreviewMouseWheel += EditorTextBox_PreviewMouseWheel;
        EditorTextBox.TextArea.Caret.PositionChanged += Caret_PositionChanged;
    }

    private void EditorTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ApplyFineVerticalScroll(EditorTextBox, e);
    }

    private void InlineDiffToggle_Changed(object sender, RoutedEventArgs e)
    {
        RefreshInlineDiffPresentation();
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        SyncNavigationToCaret();
    }

    private void PreviousDiffButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToDiff(-1);
    }

    private void NextDiffButton_Click(object sender, RoutedEventArgs e)
    {
        NavigateToDiff(1);
    }

    private void EditorTextBox_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdatingEditorFromViewModel)
            return;

        if (!string.Equals(ViewModel.EditorText, EditorTextBox.Text, StringComparison.Ordinal))
            ViewModel.EditorText = EditorTextBox.Text;

        RefreshInlineDiffPresentation();
    }

    private void UpdateEditorTextFromViewModel()
    {
        try
        {
            _isUpdatingEditorFromViewModel = true;

            if (!string.Equals(EditorTextBox.Text, ViewModel.EditorText, StringComparison.Ordinal))
                EditorTextBox.Text = ViewModel.EditorText ?? string.Empty;

            RefreshInlineDiffPresentation();
        }
        finally
        {
            _isUpdatingEditorFromViewModel = false;
        }
    }

    private void RefreshInlineDiffPresentation()
    {
        if (ViewModel is null)
            return;

        if (InlineDiffToggle.IsChecked != true || !ViewModel.CanShowDiff)
        {
            _inlineDiffLineBackgroundRenderer.Clear();
            _inlineDiffSegmentTransformer.Clear();
            _inlineDiffMarkerMargin.Clear();
            UpdateDiffNavigationState([]);
            EditorTextBox.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            EditorTextBox.TextArea.TextView.Redraw();
            return;
        }

        FileDiffViewModel diffViewModel = new(
            ViewModel.RemotePath,
            ViewModel.OriginalText,
            ViewModel.EditorText);

        HashSet<int> modifiedLineNumbers = [];
        HashSet<int> addedLineNumbers = [];
        Dictionary<int, List<(int start, int length, bool isAdded)>> segments = [];
        SortedSet<int> diffLineNumbers = [];

        foreach (FileDiffLineViewModel line in diffViewModel.CurrentLines)
        {
            if (!int.TryParse(line.LineNumberText, out int lineNumber))
                continue;

            if (line.LineKind == FileDiffLineKind.Modified)
            {
                modifiedLineNumbers.Add(lineNumber);
                diffLineNumbers.Add(lineNumber);
            }
            else if (line.LineKind == FileDiffLineKind.Added)
            {
                addedLineNumbers.Add(lineNumber);
                diffLineNumbers.Add(lineNumber);
            }

            foreach (FileDiffSegmentViewModel segment in line.Segments)
            {
                if (segment.SourceLength <= 0)
                    continue;

                bool isDifferent = segment.Kind != FileDiffSegmentKind.Unchanged;
                if (!isDifferent)
                    continue;

                if (!segments.ContainsKey(lineNumber))
                    segments[lineNumber] = [];

                bool useAddedColor = line.LineKind == FileDiffLineKind.Added;

                segments[lineNumber].Add((
                    segment.SourceStart,
                    segment.SourceLength,
                    useAddedColor
                ));
            }
        }

        _inlineDiffLineBackgroundRenderer.SetLineSets(modifiedLineNumbers, addedLineNumbers);
        _inlineDiffMarkerMargin.SetLineSets(modifiedLineNumbers, addedLineNumbers);
        EditorTextBox.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        _inlineDiffSegmentTransformer.SetSegments(segments);
        EditorTextBox.TextArea.TextView.Redraw();
        UpdateDiffNavigationState([.. diffLineNumbers]);
    }

    private void UpdateDiffNavigationState(List<int> diffLineNumbers)
    {
        _diffLineNumbers = diffLineNumbers;

        if (InlineDiffToggle.IsChecked != true || _diffLineNumbers.Count == 0)
        {
            _currentDiffIndex = -1;
            PreviousDiffButton.IsEnabled = false;
            NextDiffButton.IsEnabled = false;
            DiffNavigationText.Text = "0 / 0";
            return;
        }

        PreviousDiffButton.IsEnabled = _diffLineNumbers.Count >= 2;
        NextDiffButton.IsEnabled = _diffLineNumbers.Count >= 2;

        SyncNavigationToCaret();
    }

    private void SyncNavigationToCaret()
    {
        if (InlineDiffToggle.IsChecked != true || _diffLineNumbers.Count == 0)
        {
            _currentDiffIndex = -1;
            _caretIsExactlyOnDiffLine = false;
            DiffNavigationText.Text = "0 / 0";
            return;
        }

        int caretLine = EditorTextBox.TextArea.Caret.Line;

        _currentDiffIndex = GetNavigationIndexForLine(caretLine, out bool isExactMatch);
        _caretIsExactlyOnDiffLine = isExactMatch;

        if (_currentDiffIndex >= 0)
            DiffNavigationText.Text = $"{_currentDiffIndex + 1} / {_diffLineNumbers.Count}";
        else
            DiffNavigationText.Text = $"0 / {_diffLineNumbers.Count}";
    }

    private int GetNavigationIndexForLine(int lineNumber, out bool isExactMatch)
    {
        isExactMatch = false;

        if (_diffLineNumbers.Count == 0)
            return -1;

        for (int i = 0; i < _diffLineNumbers.Count; i++)
        {
            if (_diffLineNumbers[i] == lineNumber)
            {
                isExactMatch = true;
                return i;
            }

            if (_diffLineNumbers[i] > lineNumber)
                return i - 1;
        }

        return _diffLineNumbers.Count - 1;
    }

    private void NavigateToDiff(int direction)
    {
        if (InlineDiffToggle.IsChecked != true || _diffLineNumbers.Count == 0)
            return;

        int caretLine = EditorTextBox.TextArea.Caret.Line;
        int targetIndex;

        if (_caretIsExactlyOnDiffLine)
        {
            if (_currentDiffIndex < 0)
            {
                targetIndex = direction > 0 ? 0 : _diffLineNumbers.Count - 1;
            }
            else
            {
                targetIndex = (_currentDiffIndex + direction + _diffLineNumbers.Count) % _diffLineNumbers.Count;
            }
        }
        else
        {
            if (direction > 0)
            {
                targetIndex = -1;

                for (int i = 0; i < _diffLineNumbers.Count; i++)
                {
                    if (_diffLineNumbers[i] > caretLine)
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex < 0)
                    targetIndex = 0;
            }
            else
            {
                targetIndex = -1;

                for (int i = _diffLineNumbers.Count - 1; i >= 0; i--)
                {
                    if (_diffLineNumbers[i] < caretLine)
                    {
                        targetIndex = i;
                        break;
                    }
                }

                if (targetIndex < 0)
                    targetIndex = _diffLineNumbers.Count - 1;
            }
        }

        _currentDiffIndex = targetIndex;
        _caretIsExactlyOnDiffLine = true;

        DiffNavigationText.Text = $"{_currentDiffIndex + 1} / {_diffLineNumbers.Count}";
        ScrollToDiffLine(_diffLineNumbers[_currentDiffIndex]);
    }

    private void ScrollToDiffLine(int lineNumber)
    {
        if (lineNumber <= 0)
            return;

        EditorTextBox.ScrollToLine(lineNumber);

        if (EditorTextBox.Document is not null && lineNumber <= EditorTextBox.Document.LineCount)
        {
            DocumentLine line = EditorTextBox.Document.GetLineByNumber(lineNumber);
            EditorTextBox.TextArea.Caret.Offset = line.Offset;
            EditorTextBox.TextArea.Caret.BringCaretToView();
            EditorTextBox.Focus();
        }
    }

    private void ApplyFineVerticalScroll(DependencyObject source, MouseWheelEventArgs e)
    {
        ScrollViewer? scrollViewer = FindVisualChild<ScrollViewer>(source);
        if (scrollViewer is null)
            return;

        double deltaSteps = e.Delta / 120.0;
        double targetOffset = scrollViewer.VerticalOffset - (deltaSteps * FineEditorScrollPixelsPerDetent);

        if (targetOffset < 0)
            targetOffset = 0;
        else if (targetOffset > scrollViewer.ScrollableHeight)
            targetOffset = scrollViewer.ScrollableHeight;

        scrollViewer.ScrollToVerticalOffset(targetOffset);
        e.Handled = true;
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