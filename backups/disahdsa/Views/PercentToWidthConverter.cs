using System;
using System.Globalization;
using System.Windows.Data;

namespace DailyDash.Views
{
    /// <summary>
    /// Converts a completion percentage (0–100) and a parent width into a pixel width for the progress bar.
    /// Usage: MultiBinding with values[0] = CompletionPercent (int), values[1] = ActualWidth (double).
    /// </summary>
    public class PercentToWidthConverter : IMultiValueConverter
    {
        public static readonly PercentToWidthConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 &&
                values[0] is int percent &&
                values[1] is double parentWidth &&
                parentWidth > 0)
            {
                return Math.Max(0, Math.Min(parentWidth, parentWidth * percent / 100.0));
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
