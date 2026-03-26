using System;
using System.Collections;
using MibExplorer.Models;

namespace MibExplorer.Core
{
    public sealed class RemoteExplorerItemComparer : IComparer
    {
        private readonly string _columnName;
        private readonly bool _ascending;

        public RemoteExplorerItemComparer(string columnName, bool ascending)
        {
            _columnName = columnName ?? "Name";
            _ascending = ascending;
        }

        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y))
                return 0;

            if (x is not RemoteExplorerItem left)
                return -1;

            if (y is not RemoteExplorerItem right)
                return 1;

            // Dossiers d'abord
            int directoryCompare = GetDirectoryRank(left).CompareTo(GetDirectoryRank(right));
            if (directoryCompare != 0)
                return directoryCompare;

            int result = _columnName.ToLowerInvariant() switch
            {
                "type" => CompareText(left.TypeLabel, right.TypeLabel),
                "size" => Nullable.Compare<long>(left.Size, right.Size),
                "modified" => Nullable.Compare<DateTimeOffset>(left.ModifiedAt, right.ModifiedAt),
                _ => CompareText(left.Name, right.Name)
            };

            if (result == 0)
                result = CompareText(left.Name, right.Name);

            return _ascending ? result : -result;
        }

        private static int GetDirectoryRank(RemoteExplorerItem item) => item.IsDirectory ? 0 : 1;

        private static int CompareText(string? left, string? right)
            => NaturalStringComparer.Instance.Compare(left ?? string.Empty, right ?? string.Empty);
    }
}
