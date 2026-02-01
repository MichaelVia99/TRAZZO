using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Bitacora.Converters
{
    public class PathToIsImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (path.Contains('|'))
            {
                var parts = path.Split('|', StringSplitOptions.None);
                path = parts.Length >= 2 ? parts[1] : parts[0];
            }

            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif" || ext == ".webp";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
