using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Windows;
using System.Windows.Media;

namespace MibExplorer.Views.FileEditor;

public sealed class FileEditorInlineDiffMarkerMargin : AbstractMargin
{
    private const double MarginWidthValue = 10.0;
    private const double MarkerWidth = 4.0;

    private static readonly Brush ModifiedMarkerBrush = CreateFrozenBrush(Color.FromArgb(0xFF, 0xFC, 0xD3, 0x4D));
    private static readonly Brush AddedMarkerBrush = CreateFrozenBrush(Color.FromArgb(0xFF, 0x36, 0xC2, 0x75));
    private static readonly Pen SeparatorPen = CreateFrozenPen(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF));

    private HashSet<int> _modifiedLineNumbers = [];
    private HashSet<int> _addedLineNumbers = [];

    public FileEditorInlineDiffMarkerMargin()
    {
        IsHitTestVisible = false;
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.ScrollOffsetChanged -= TextView_ScrollOffsetChanged;
            oldTextView.VisualLinesChanged -= TextView_VisualLinesChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);

        if (newTextView is not null)
        {
            newTextView.ScrollOffsetChanged += TextView_ScrollOffsetChanged;
            newTextView.VisualLinesChanged += TextView_VisualLinesChanged;
        }

        InvalidateVisual();
    }

    private void TextView_ScrollOffsetChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    private void TextView_VisualLinesChanged(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }

    public void SetLineSets(IEnumerable<int> modifiedLineNumbers, IEnumerable<int> addedLineNumbers)
    {
        _modifiedLineNumbers = [.. modifiedLineNumbers];
        _addedLineNumbers = [.. addedLineNumbers];
        InvalidateVisual();
    }

    public void Clear()
    {
        _modifiedLineNumbers.Clear();
        _addedLineNumbers.Clear();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return new Size(MarginWidthValue, 0);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        TextView? textView = TextView;
        if (textView is null || !textView.VisualLinesValid)
            return;

        double separatorX = MarginWidthValue - 0.5;
        drawingContext.DrawLine(SeparatorPen, new Point(separatorX, 0), new Point(separatorX, RenderSize.Height));

        foreach (VisualLine visualLine in textView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;

            Brush? markerBrush = null;

            if (_addedLineNumbers.Contains(lineNumber))
                markerBrush = AddedMarkerBrush;
            else if (_modifiedLineNumbers.Contains(lineNumber))
                markerBrush = ModifiedMarkerBrush;

            if (markerBrush is null)
                continue;

            Rect markerRect = new(
                MarginWidthValue - MarkerWidth - 2.0,
                visualLine.VisualTop - textView.VerticalOffset,
                MarkerWidth,
                visualLine.Height);

            drawingContext.DrawRectangle(markerBrush, null, markerRect);
        }
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreateFrozenPen(Color color)
    {
        Pen pen = new(new SolidColorBrush(color), 1);
        pen.Freeze();
        return pen;
    }
}