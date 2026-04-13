using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace MibExplorer.Views.FileEditor;

public sealed class FileEditorInlineDiffLineBackgroundRenderer : IBackgroundRenderer
{
    private static readonly Brush ModifiedLineBrush = CreateFrozenBrush(Color.FromArgb(0x22, 0xD6, 0x9E, 0x2E));
    private static readonly Brush AddedLineBrush = CreateFrozenBrush(Color.FromArgb(0x22, 0x36, 0xC2, 0x75));

    private HashSet<int> _modifiedLineNumbers = [];
    private HashSet<int> _addedLineNumbers = [];

    public KnownLayer Layer => KnownLayer.Background;

    public void SetLineSets(IEnumerable<int> modifiedLineNumbers, IEnumerable<int> addedLineNumbers)
    {
        _modifiedLineNumbers = [.. modifiedLineNumbers];
        _addedLineNumbers = [.. addedLineNumbers];
    }

    public void Clear()
    {
        _modifiedLineNumbers.Clear();
        _addedLineNumbers.Clear();
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid)
            return;

        foreach (VisualLine visualLine in textView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;

            Brush? brush = null;

            if (_addedLineNumbers.Contains(lineNumber))
                brush = AddedLineBrush;
            else if (_modifiedLineNumbers.Contains(lineNumber))
                brush = ModifiedLineBrush;

            if (brush is null)
                continue;

            DocumentLine documentLine = visualLine.FirstDocumentLine;

            if (documentLine.Length == 0)
            {
                Rect emptyLineRect = new(0, visualLine.VisualTop - textView.VerticalOffset, textView.ActualWidth, visualLine.Height);
                drawingContext.DrawRectangle(brush, null, emptyLineRect);
                continue;
            }

            TextSegment segment = new()
            {
                StartOffset = documentLine.Offset,
                Length = documentLine.Length
            };

            foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                Rect fullWidthRect = new(0, rect.Top, textView.ActualWidth, rect.Height);
                drawingContext.DrawRectangle(brush, null, fullWidthRect);
            }
        }
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }
}