using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MemoryLeakDetector.UI.Converters
{
    public sealed class BooleanToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = new SolidColorBrush(Color.FromRgb(220, 53, 69));
        public Brush FalseBrush { get; set; } = new SolidColorBrush(Color.FromRgb(40, 167, 69));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool flag)
            {
                return flag ? TrueBrush : FalseBrush;
            }

            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}

