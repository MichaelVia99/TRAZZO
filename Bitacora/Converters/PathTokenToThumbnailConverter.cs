using System;
using System.Globalization;
using System.Windows.Data;

namespace Bitacora.Converters
{
    public class PathTokenToThumbnailConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (path.Contains("|"))
            {
                var parts = path.Split('|', StringSplitOptions.None);
                if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    return parts[0];
                }
            }

            return path;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

