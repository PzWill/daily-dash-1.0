using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace DailyDash.Controls
{
    public class ValueToAngleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 || !(values[0] is double value) || !(values[1] is double radiusX) || !(values[2] is double radiusY))
                return new Point(50, 0); // Default pointing to top

            // Clamp value between 0 and 100
            value = Math.Max(0, Math.Min(100, value));
            
            // To avoid the point falling exactly on the start point at 100% and disappearing
            if (value >= 99.99) value = 99.99;

            double angleInDegrees = (value / 100.0) * 360.0;
            double angleInRadians = angleInDegrees * (Math.PI / 180.0);

            // Since we rotate the path -90 degrees in XAML, 0 degrees is mathematically the X axis (X=1, Y=0).
            // Parametric equation for circle: X = cx + r * cos(a), Y = cy + r * sin(a)
            // Center is 50,50
            double x = 50.0 + (radiusX * Math.Cos(angleInRadians));
            double y = 50.0 + (radiusY * Math.Sin(angleInRadians));

            return new Point(x, y);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ValueToLargeArcConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double val)
            {
                return val > 50.0; // IsLargeArc is true if the angle is > 180 degrees (i.e. > 50%)
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
