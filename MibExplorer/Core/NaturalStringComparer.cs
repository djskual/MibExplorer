using System;
using System.Collections.Generic;
using System.Globalization;

namespace MibExplorer.Core
{
    public sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y))
                return 0;

            if (x is null)
                return -1;

            if (y is null)
                return 1;

            int ix = 0;
            int iy = 0;

            while (ix < x.Length && iy < y.Length)
            {
                char cx = x[ix];
                char cy = y[iy];

                bool xIsDigit = char.IsDigit(cx);
                bool yIsDigit = char.IsDigit(cy);

                if (xIsDigit && yIsDigit)
                {
                    int startX = ix;
                    int startY = iy;

                    while (ix < x.Length && char.IsDigit(x[ix]))
                        ix++;

                    while (iy < y.Length && char.IsDigit(y[iy]))
                        iy++;

                    string numX = x[startX..ix];
                    string numY = y[startY..iy];

                    string trimmedX = numX.TrimStart('0');
                    string trimmedY = numY.TrimStart('0');

                    if (trimmedX.Length == 0) trimmedX = "0";
                    if (trimmedY.Length == 0) trimmedY = "0";

                    if (trimmedX.Length != trimmedY.Length)
                        return trimmedX.Length.CompareTo(trimmedY.Length);

                    int numberCompare = string.CompareOrdinal(trimmedX, trimmedY);
                    if (numberCompare != 0)
                        return numberCompare;

                    if (numX.Length != numY.Length)
                        return numX.Length.CompareTo(numY.Length);

                    continue;
                }

                int charCompare = char.ToUpper(cx, CultureInfo.InvariantCulture)
                    .CompareTo(char.ToUpper(cy, CultureInfo.InvariantCulture));

                if (charCompare != 0)
                    return charCompare;

                ix++;
                iy++;
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
