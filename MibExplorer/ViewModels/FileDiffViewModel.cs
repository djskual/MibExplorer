using MibExplorer.Core;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace MibExplorer.ViewModels;

public sealed class FileDiffViewModel : ObservableObject
{
    private readonly string _originalSourceText;
    private readonly string _currentSourceText;

    private int _currentChangedLineListIndex = -1;
    private bool _ignoreWhitespace;
    private bool _showInvisibles;
    private bool _collapseUnchanged;

    private ReadOnlyCollection<FileDiffLineViewModel> _originalLines =
        new(new List<FileDiffLineViewModel>());

    private ReadOnlyCollection<FileDiffLineViewModel> _currentLines =
        new(new List<FileDiffLineViewModel>());

    private ReadOnlyCollection<int> _changedLineIndices =
        new(new List<int>());

    private string _summaryText = "No differences.";

    public FileDiffViewModel(string remotePath, string originalText, string currentText)
    {
        RemotePath = remotePath;
        WindowTitle = $"Diff - {remotePath}";

        _originalSourceText = originalText ?? string.Empty;
        _currentSourceText = currentText ?? string.Empty;

        RefreshDiff();
    }

    public string RemotePath { get; }

    public string WindowTitle { get; }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public ReadOnlyCollection<FileDiffLineViewModel> OriginalLines
    {
        get => _originalLines;
        private set => SetProperty(ref _originalLines, value);
    }

    public ReadOnlyCollection<FileDiffLineViewModel> CurrentLines
    {
        get => _currentLines;
        private set => SetProperty(ref _currentLines, value);
    }

    public ReadOnlyCollection<int> ChangedLineIndices
    {
        get => _changedLineIndices;
        private set
        {
            if (SetProperty(ref _changedLineIndices, value))
                OnPropertyChanged(nameof(DiffPositionText));
        }
    }

    public bool IgnoreWhitespace
    {
        get => _ignoreWhitespace;
        set
        {
            if (SetProperty(ref _ignoreWhitespace, value))
                RefreshDiff();
        }
    }

    public bool ShowInvisibles
    {
        get => _showInvisibles;
        set
        {
            if (SetProperty(ref _showInvisibles, value))
                RefreshDiff();
        }
    }

    public bool CollapseUnchanged
    {
        get => _collapseUnchanged;
        set
        {
            if (SetProperty(ref _collapseUnchanged, value))
                RefreshDiff();
        }
    }

    public int CurrentChangedLineListIndex
    {
        get => _currentChangedLineListIndex;
        private set
        {
            if (SetProperty(ref _currentChangedLineListIndex, value))
                OnPropertyChanged(nameof(DiffPositionText));
        }
    }

    public string DiffPositionText
    {
        get
        {
            if (ChangedLineIndices.Count == 0)
                return "Difference 0 of 0";

            if (CurrentChangedLineListIndex < 0 || CurrentChangedLineListIndex >= ChangedLineIndices.Count)
                return $"Difference 0 of {ChangedLineIndices.Count}";

            return $"Difference {CurrentChangedLineListIndex + 1} of {ChangedLineIndices.Count}";
        }
    }

    public void UpdateCurrentDiffPosition(int changedLineListIndex)
    {
        CurrentChangedLineListIndex = changedLineListIndex;
    }

    private void RefreshDiff()
    {
        List<AlignedRow> alignedRows = BuildAlignedRows();
        int diffCount = alignedRows.Count(r => r.IsChanged);

        List<FileDiffLineViewModel> visibleLeft = [];
        List<FileDiffLineViewModel> visibleRight = [];
        List<int> changedVisibleIndices = [];

        int i = 0;
        while (i < alignedRows.Count)
        {
            if (!CollapseUnchanged || alignedRows[i].IsChanged)
            {
                AddAlignedRow(alignedRows[i], visibleLeft, visibleRight, changedVisibleIndices);
                i++;
                continue;
            }

            int runStart = i;
            while (i < alignedRows.Count && !alignedRows[i].IsChanged)
                i++;

            int runLength = i - runStart;

            if (runLength <= 4)
            {
                for (int j = runStart; j < i; j++)
                    AddAlignedRow(alignedRows[j], visibleLeft, visibleRight, changedVisibleIndices);

                continue;
            }

            AddAlignedRow(alignedRows[runStart], visibleLeft, visibleRight, changedVisibleIndices);

            int hiddenCount = runLength - 2;
            visibleLeft.Add(FileDiffLineViewModel.CreateCollapsed(hiddenCount));
            visibleRight.Add(FileDiffLineViewModel.CreateCollapsed(hiddenCount));

            AddAlignedRow(alignedRows[i - 1], visibleLeft, visibleRight, changedVisibleIndices);
        }

        ApplyGitLikeMarkers(visibleLeft);
        ApplyGitLikeMarkers(visibleRight);

        OriginalLines = new ReadOnlyCollection<FileDiffLineViewModel>(visibleLeft);
        CurrentLines = new ReadOnlyCollection<FileDiffLineViewModel>(visibleRight);
        ChangedLineIndices = new ReadOnlyCollection<int>(changedVisibleIndices);

        SummaryText = diffCount == 0
            ? "No differences."
            : $"{diffCount} line(s) differ.";

        if (ChangedLineIndices.Count == 0)
        {
            UpdateCurrentDiffPosition(-1);
        }
        else
        {
            int nextIndex = CurrentChangedLineListIndex;
            if (nextIndex < 0 || nextIndex >= ChangedLineIndices.Count)
                nextIndex = 0;

            UpdateCurrentDiffPosition(nextIndex);
        }
    }

    private List<AlignedRow> BuildAlignedRows()
    {
        string[] originalLines = SplitLines(_originalSourceText);
        string[] currentLines = SplitLines(_currentSourceText);

        string[] originalKeys = originalLines.Select(l => NormalizeLineForMatch(l, IgnoreWhitespace)).ToArray();
        string[] currentKeys = currentLines.Select(l => NormalizeLineForMatch(l, IgnoreWhitespace)).ToArray();

        List<AlignedPair> pairs = BuildLineAlignment(originalKeys, currentKeys);
        pairs = MergeSimilarAdjacentChanges(pairs, originalLines, currentLines);

        List<AlignedRow> rows = new(pairs.Count);

        foreach (AlignedPair pair in pairs)
        {
            bool hasLeft = pair.LeftIndex >= 0;
            bool hasRight = pair.RightIndex >= 0;

            string leftText = hasLeft ? originalLines[pair.LeftIndex] : string.Empty;
            string rightText = hasRight ? currentLines[pair.RightIndex] : string.Empty;

            string leftNumber = hasLeft ? (pair.LeftIndex + 1).ToString() : string.Empty;
            string rightNumber = hasRight ? (pair.RightIndex + 1).ToString() : string.Empty;

            bool isChanged = !(hasLeft && hasRight &&
                               AreLinesEquivalent(leftText, rightText, IgnoreWhitespace));

            FileDiffLineKind leftKind;
            FileDiffLineKind rightKind;

            if (hasLeft && !hasRight)
            {
                leftKind = FileDiffLineKind.Removed;
                rightKind = FileDiffLineKind.RemovedPairPlaceholder;
            }
            else if (!hasLeft && hasRight)
            {
                leftKind = FileDiffLineKind.AddedPairPlaceholder;
                rightKind = FileDiffLineKind.Added;
            }
            else if (isChanged)
            {
                leftKind = FileDiffLineKind.Modified;
                rightKind = FileDiffLineKind.Modified;
            }
            else
            {
                leftKind = FileDiffLineKind.Unchanged;
                rightKind = FileDiffLineKind.Unchanged;
            }

            BuildSegments(
                leftText,
                rightText,
                IgnoreWhitespace,
                ShowInvisibles,
                out ReadOnlyCollection<FileDiffSegmentViewModel> leftSegments,
                out ReadOnlyCollection<FileDiffSegmentViewModel> rightSegments);

            rows.Add(new AlignedRow(
                isChanged,
                new FileDiffLineViewModel(
                    lineNumberText: leftNumber,
                    text: leftText,
                    lineKind: leftKind,
                    segments: leftSegments,
                    changeMarker: string.Empty),
                new FileDiffLineViewModel(
                    lineNumberText: rightNumber,
                    text: rightText,
                    lineKind: rightKind,
                    segments: rightSegments,
                    changeMarker: string.Empty)));
        }

        return rows;
    }

    private static List<AlignedPair> BuildLineAlignment(string[] leftKeys, string[] rightKeys)
    {
        int m = leftKeys.Length;
        int n = rightKeys.Length;

        int[,] dp = new int[m + 1, n + 1];

        for (int i = m - 1; i >= 0; i--)
        {
            for (int j = n - 1; j >= 0; j--)
            {
                if (string.Equals(leftKeys[i], rightKeys[j], StringComparison.Ordinal))
                    dp[i, j] = dp[i + 1, j + 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        List<AlignedPair> pairs = [];
        int li = 0;
        int ri = 0;

        while (li < m && ri < n)
        {
            if (string.Equals(leftKeys[li], rightKeys[ri], StringComparison.Ordinal))
            {
                pairs.Add(new AlignedPair(li, ri));
                li++;
                ri++;
            }
            else if (dp[li + 1, ri] >= dp[li, ri + 1])
            {
                pairs.Add(new AlignedPair(li, -1));
                li++;
            }
            else
            {
                pairs.Add(new AlignedPair(-1, ri));
                ri++;
            }
        }

        while (li < m)
        {
            pairs.Add(new AlignedPair(li, -1));
            li++;
        }

        while (ri < n)
        {
            pairs.Add(new AlignedPair(-1, ri));
            ri++;
        }

        return pairs;
    }

    private static List<AlignedPair> MergeSimilarAdjacentChanges(
    List<AlignedPair> pairs,
    string[] originalLines,
    string[] currentLines)
    {
        List<AlignedPair> merged = [];
        int i = 0;

        while (i < pairs.Count)
        {
            if (i < pairs.Count - 1 &&
                pairs[i].LeftIndex >= 0 &&
                pairs[i].RightIndex < 0 &&
                pairs[i + 1].LeftIndex < 0 &&
                pairs[i + 1].RightIndex >= 0)
            {
                string leftText = originalLines[pairs[i].LeftIndex];
                string rightText = currentLines[pairs[i + 1].RightIndex];

                if (AreLinesSimilar(leftText, rightText))
                {
                    merged.Add(new AlignedPair(pairs[i].LeftIndex, pairs[i + 1].RightIndex));
                    i += 2;
                    continue;
                }
            }

            if (i < pairs.Count - 1 &&
                pairs[i].LeftIndex < 0 &&
                pairs[i].RightIndex >= 0 &&
                pairs[i + 1].LeftIndex >= 0 &&
                pairs[i + 1].RightIndex < 0)
            {
                string leftText = originalLines[pairs[i + 1].LeftIndex];
                string rightText = currentLines[pairs[i].RightIndex];

                if (AreLinesSimilar(leftText, rightText))
                {
                    merged.Add(new AlignedPair(pairs[i + 1].LeftIndex, pairs[i].RightIndex));
                    i += 2;
                    continue;
                }
            }

            merged.Add(pairs[i]);
            i++;
        }

        return merged;
    }

    private static bool AreLinesSimilar(string leftText, string rightText)
    {
        string left = NormalizeWhitespaceForSimilarity(leftText);
        string right = NormalizeWhitespaceForSimilarity(rightText);

        if (left.Length == 0 && right.Length == 0)
            return true;

        if (left.Length == 0 || right.Length == 0)
            return false;

        int distance = ComputeLevenshteinDistance(left, right);
        int maxLength = Math.Max(left.Length, right.Length);

        double similarity = 1.0 - ((double)distance / maxLength);

        return similarity >= 0.60;
    }

    private static string NormalizeWhitespaceForSimilarity(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static int ComputeLevenshteinDistance(string left, string right)
    {
        int m = left.Length;
        int n = right.Length;

        int[,] dp = new int[m + 1, n + 1];

        for (int i = 0; i <= m; i++)
            dp[i, 0] = i;

        for (int j = 0; j <= n; j++)
            dp[0, j] = j;

        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                int cost = left[i - 1] == right[j - 1] ? 0 : 1;

                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[m, n];
    }

    private static void AddAlignedRow(
        AlignedRow row,
        List<FileDiffLineViewModel> visibleLeft,
        List<FileDiffLineViewModel> visibleRight,
        List<int> changedVisibleIndices)
    {
        int visibleIndex = visibleLeft.Count;

        visibleLeft.Add(row.LeftLine);
        visibleRight.Add(row.RightLine);

        if (row.IsChanged)
            changedVisibleIndices.Add(visibleIndex);
    }

    private static void ApplyGitLikeMarkers(List<FileDiffLineViewModel> lines)
    {
        FileDiffLineKind? previousMarkerKind = null;

        for (int i = 0; i < lines.Count; i++)
        {
            FileDiffLineViewModel line = lines[i];

            if (!IsMarkerKind(line.LineKind))
            {
                line.ChangeMarker = string.Empty;
                previousMarkerKind = null;
                continue;
            }

            if (previousMarkerKind == line.LineKind)
            {
                line.ChangeMarker = string.Empty;
            }
            else
            {
                line.ChangeMarker = GetDefaultMarker(line.LineKind);
                previousMarkerKind = line.LineKind;
            }
        }
    }

    private static bool IsMarkerKind(FileDiffLineKind kind)
    {
        return kind is FileDiffLineKind.Added or FileDiffLineKind.Removed or FileDiffLineKind.Modified;
    }

    private static string GetDefaultMarker(FileDiffLineKind kind)
    {
        return kind switch
        {
            FileDiffLineKind.Added => "+",
            FileDiffLineKind.Removed => "-",
            FileDiffLineKind.Modified => "~",
            _ => string.Empty
        };
    }

    private static string NormalizeLineForMatch(string text, bool ignoreWhitespace)
    {
        if (!ignoreWhitespace)
            return text ?? string.Empty;

        return NormalizeWhitespaceForCompare(text);
    }

    private static bool AreLinesEquivalent(string leftText, string rightText, bool ignoreWhitespace)
    {
        if (!ignoreWhitespace)
            return string.Equals(leftText, rightText, StringComparison.Ordinal);

        return string.Equals(
            NormalizeWhitespaceForCompare(leftText),
            NormalizeWhitespaceForCompare(rightText),
            StringComparison.Ordinal);
    }

    private static string NormalizeWhitespaceForCompare(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string[] SplitLines(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static void BuildSegments(
        string leftText,
        string rightText,
        bool ignoreWhitespace,
        bool showInvisibles,
        out ReadOnlyCollection<FileDiffSegmentViewModel> leftSegments,
        out ReadOnlyCollection<FileDiffSegmentViewModel> rightSegments)
    {
        List<DiffToken> leftTokens = Tokenize(leftText, showInvisibles);
        List<DiffToken> rightTokens = Tokenize(rightText, showInvisibles);

        bool[] leftUnchanged = new bool[leftTokens.Count];
        bool[] rightUnchanged = new bool[rightTokens.Count];

        MarkCommonTokens(leftTokens, rightTokens, leftUnchanged, rightUnchanged);

        if (ignoreWhitespace)
        {
            for (int i = 0; i < leftTokens.Count; i++)
            {
                if (leftTokens[i].IsWhitespace)
                    leftUnchanged[i] = true;
            }

            for (int i = 0; i < rightTokens.Count; i++)
            {
                if (rightTokens[i].IsWhitespace)
                    rightUnchanged[i] = true;
            }
        }

        leftSegments = BuildSegmentCollection(
            leftTokens,
            leftUnchanged,
            unchangedKind: FileDiffSegmentKind.Unchanged,
            changedKind: FileDiffSegmentKind.Removed,
            showInvisibles: showInvisibles);

        rightSegments = BuildSegmentCollection(
            rightTokens,
            rightUnchanged,
            unchangedKind: FileDiffSegmentKind.Unchanged,
            changedKind: FileDiffSegmentKind.Added,
            showInvisibles: showInvisibles);
    }

    private static List<DiffToken> Tokenize(string text, bool showInvisibles)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        MatchCollection matches = Regex.Matches(text, @"\w+|\s+|[^\w\s]", RegexOptions.CultureInvariant);
        List<DiffToken> tokens = new(matches.Count);

        foreach (Match match in matches)
        {
            string tokenText = match.Value;
            bool isWhitespace = tokenText.All(char.IsWhiteSpace);

            tokens.Add(new DiffToken(tokenText, isWhitespace));
        }

        return tokens;
    }

    private static void MarkCommonTokens(
        List<DiffToken> leftTokens,
        List<DiffToken> rightTokens,
        bool[] leftUnchanged,
        bool[] rightUnchanged)
    {
        int m = leftTokens.Count;
        int n = rightTokens.Count;

        int[,] dp = new int[m + 1, n + 1];

        for (int i = m - 1; i >= 0; i--)
        {
            for (int j = n - 1; j >= 0; j--)
            {
                if (string.Equals(leftTokens[i].CompareText, rightTokens[j].CompareText, StringComparison.Ordinal))
                    dp[i, j] = dp[i + 1, j + 1] + 1;
                else
                    dp[i, j] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
            }
        }

        int li = 0;
        int ri = 0;

        while (li < m && ri < n)
        {
            if (string.Equals(leftTokens[li].CompareText, rightTokens[ri].CompareText, StringComparison.Ordinal))
            {
                leftUnchanged[li] = true;
                rightUnchanged[ri] = true;
                li++;
                ri++;
            }
            else if (dp[li + 1, ri] >= dp[li, ri + 1])
            {
                li++;
            }
            else
            {
                ri++;
            }
        }
    }

    private static ReadOnlyCollection<FileDiffSegmentViewModel> BuildSegmentCollection(
    List<DiffToken> tokens,
    bool[] unchangedFlags,
    FileDiffSegmentKind unchangedKind,
    FileDiffSegmentKind changedKind,
    bool showInvisibles)
    {
        List<FileDiffSegmentViewModel> segments = new(tokens.Count);
        int visualColumn = 0;

        for (int i = 0; i < tokens.Count; i++)
        {
            string displayText = ExpandTextPreservingAlignment(
                tokens[i].CompareText,
                ref visualColumn,
                showInvisibles);

            segments.Add(new FileDiffSegmentViewModel(
                displayText,
                unchangedFlags[i] ? unchangedKind : changedKind));
        }

        if (segments.Count == 0)
            segments.Add(new FileDiffSegmentViewModel(string.Empty, FileDiffSegmentKind.Unchanged));

        return new ReadOnlyCollection<FileDiffSegmentViewModel>(segments);
    }

    private const int DiffTabSize = 4;

    private static string ExpandTextPreservingAlignment(string text, ref int visualColumn, bool showInvisibles)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        StringBuilder builder = new();

        foreach (char c in text)
        {
            switch (c)
            {
                case ' ':
                    builder.Append(showInvisibles ? '·' : ' ');
                    visualColumn++;
                    break;

                case '\t':
                    int width = DiffTabSize - (visualColumn % DiffTabSize);
                    if (width == 0)
                        width = DiffTabSize;

                    if (showInvisibles)
                    {
                        builder.Append('⇥');

                        for (int i = 1; i < width; i++)
                            builder.Append('·');
                    }
                    else
                    {
                        for (int i = 0; i < width; i++)
                            builder.Append(' ');
                    }

                    visualColumn += width;
                    break;

                default:
                    builder.Append(c);
                    visualColumn++;
                    break;
            }
        }

        return builder.ToString();
    }

    private sealed record DiffToken(string CompareText, bool IsWhitespace);
    private sealed record AlignedPair(int LeftIndex, int RightIndex);
    private sealed record AlignedRow(bool IsChanged, FileDiffLineViewModel LeftLine, FileDiffLineViewModel RightLine);
}

public sealed class FileDiffLineViewModel
{
    public FileDiffLineViewModel(
        string lineNumberText,
        string text,
        FileDiffLineKind lineKind,
        ReadOnlyCollection<FileDiffSegmentViewModel> segments,
        string changeMarker)
    {
        LineNumberText = lineNumberText;
        Text = text ?? string.Empty;
        LineKind = lineKind;
        Segments = segments;
        ChangeMarker = changeMarker;
    }

    public string LineNumberText { get; }

    public string Text { get; }

    public FileDiffLineKind LineKind { get; }

    public ReadOnlyCollection<FileDiffSegmentViewModel> Segments { get; }

    public string ChangeMarker { get; set; }

    public bool IsPlaceholder =>
        LineKind == FileDiffLineKind.AddedPairPlaceholder ||
        LineKind == FileDiffLineKind.RemovedPairPlaceholder ||
        LineKind == FileDiffLineKind.Collapsed;

    public string PlaceholderText => LineKind switch
    {
        FileDiffLineKind.AddedPairPlaceholder => "— no line —",
        FileDiffLineKind.RemovedPairPlaceholder => "— no line —",
        FileDiffLineKind.Collapsed => Text,
        _ => string.Empty
    };

    public static FileDiffLineViewModel CreateCollapsed(int hiddenLineCount)
    {
        return new FileDiffLineViewModel(
            lineNumberText: string.Empty,
            text: $"… {hiddenLineCount} unchanged line(s) …",
            lineKind: FileDiffLineKind.Collapsed,
            segments: new ReadOnlyCollection<FileDiffSegmentViewModel>(
                [new FileDiffSegmentViewModel(string.Empty, FileDiffSegmentKind.Unchanged)]),
            changeMarker: string.Empty);
    }
}

public sealed class FileDiffSegmentViewModel
{
    public FileDiffSegmentViewModel(string text, FileDiffSegmentKind kind)
    {
        Text = text ?? string.Empty;
        Kind = kind;
    }

    public string Text { get; }

    public FileDiffSegmentKind Kind { get; }
}

public enum FileDiffSegmentKind
{
    Unchanged,
    Added,
    Removed
}

public enum FileDiffLineKind
{
    Unchanged,
    Modified,
    Added,
    Removed,
    AddedPairPlaceholder,
    RemovedPairPlaceholder,
    Collapsed
}