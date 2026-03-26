using System;
using System.Globalization;
using System.Windows.Data;

namespace MibExplorer.Core
{
    public class HeaderMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2)
                return false;

            var active = values[0]?.ToString();
            var header = values[1]?.ToString();

            if (string.IsNullOrWhiteSpace(active) || string.IsNullOrWhiteSpace(header))
                return false;

            return string.Equals(active, header, StringComparison.OrdinalIgnoreCase);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
