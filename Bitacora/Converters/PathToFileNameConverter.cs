using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Bitacora.Converters
{
    public class PathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (path.Contains('|'))
            {
                var parts = path.Split('|', StringSplitOptions.None);
                
                // Heuristic: Check if parts[1] is a number (Size) to distinguish formats
                // Format 1: full|size|name (3 parts) or full|size (2 parts)
                // Format 2: mini|full|size|name (4 parts) or mini|full...
                
                bool isPart1Size = parts.Length >= 2 && int.TryParse(parts[1], out _);

                if (isPart1Size)
                {
                    // full|size|name
                    if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                    {
                        return parts[2];
                    }
                    path = parts[0];
                }
                else
                {
                    // mini|full|size|name
                    if (parts.Length >= 4 && !string.IsNullOrWhiteSpace(parts[3]))
                    {
                        return parts[3];
                    }
                    path = parts.Length >= 2 ? parts[1] : parts[0];
                }
            }

            return Path.GetFileName(path);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
