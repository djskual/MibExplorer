using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Collections.Generic;
using System.Windows.Media;

namespace MibExplorer.Views.FileEditor;

public sealed class FileEditorInlineDiffSegmentTransformer : DocumentColorizingTransformer
{
    private static readonly Brush ModifiedSegmentBrush = CreateFrozenBrush(Color.FromArgb(0x66, 0xD6, 0x9E, 0x2E));
    private static readonly Brush AddedSegmentBrush = CreateFrozenBrush(Color.FromArgb(0x66, 0x36, 0xC2, 0x75));

    private readonly Dictionary<int, List<(int start, int length, bool isAdded)>> _segmentsByLine = [];

    public void SetSegments(Dictionary<int, List<(int start, int length, bool isAdded)>> segments)
    {
        _segmentsByLine.Clear();

        foreach (var kvp in segments)
            _segmentsByLine[kvp.Key] = kvp.Value;
    }

    public void Clear()
    {
        _segmentsByLine.Clear();
    }

    protected override void ColorizeLine(DocumentLine line)
    {
        if (!_segmentsByLine.TryGetValue(line.LineNumber, out var segments))
            return;

        foreach (var segment in segments)
        {
            if (segment.length <= 0)
                continue;

            int startOffset = line.Offset + segment.start;
            int endOffset = startOffset + segment.length;

            ChangeLinePart(startOffset, endOffset, element =>
            {
                element.TextRunProperties.SetBackgroundBrush(
                    segment.isAdded ? AddedSegmentBrush : ModifiedSegmentBrush);
            });
        }
    }

    private static Brush CreateFrozenBrush(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }
}