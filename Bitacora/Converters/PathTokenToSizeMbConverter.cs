using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Bitacora.Converters
{
    public class PathTokenToSizeMbConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var path = value as string;
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string originalPath = path;
            int sizeKb = 0;

            if (path.Contains('|'))
            {
                var parts = path.Split('|', StringSplitOptions.None);
                
                // Heuristic: Check if parts[1] is a number (Size)
                int s1 = 0;
                bool isPart1Size = parts.Length >= 2 && int.TryParse(parts[1], out s1);

                if (isPart1Size)
                {
                    // Format: full|size|name
                    originalPath = parts[0];
                    sizeKb = s1;
                }
                else
                {
                    // Format: mini|full|size|name
                    if (parts.Length >= 2)
                    {
                        originalPath = parts[1];
                    }
                    else
                    {
                        originalPath = parts[0];
                    }

                    if (parts.Length >= 3)
                    {
                        int.TryParse(parts[2], out sizeKb);
                    }
                }
            }

            long sizeBytes = 0;

            if (sizeKb > 0)
            {
                sizeBytes = sizeKb * 1024L;
            }
            else if (!IsHttpUrl(originalPath) && File.Exists(originalPath))
            {
                var fi = new FileInfo(originalPath);
                sizeBytes = fi.Length;
            }

            if (sizeBytes <= 0)
            {
                return string.Empty;
            }

            double mb = sizeBytes / (1024.0 * 1024.0);
            return mb.ToString("0.00") + " MB";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static bool IsHttpUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}

