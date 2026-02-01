using System;
using System.Globalization;
using System.Windows.Data;

namespace Bitacora.Converters
{
    public class MathConverter : IValueConverter
    {
        public string Operation { get; set; } = "Divide";
        public double Factor { get; set; } = 1.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                switch (Operation)
                {
                    case "Add": return d + Factor;
                    case "Subtract": return d - Factor;
                    case "Multiply": return d * Factor;
                    case "Divide": return Factor != 0 ? d / Factor : d;
                    default: return d;
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
