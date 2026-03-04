using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace DailyDash.Controls
{
    public class CircularProgressBar : Control
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(CircularProgressBar), 
                new PropertyMetadata(0d, OnValueChanged));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register("StrokeThickness", typeof(double), typeof(CircularProgressBar), 
                new PropertyMetadata(10d));

        public static readonly DependencyProperty ProgressBrushProperty =
            DependencyProperty.Register("ProgressBrush", typeof(Brush), typeof(CircularProgressBar), 
                new PropertyMetadata(Brushes.White));

        public static readonly DependencyProperty TrackBrushProperty =
            DependencyProperty.Register("TrackBrush", typeof(Brush), typeof(CircularProgressBar), 
                new PropertyMetadata(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))));

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public double StrokeThickness
        {
            get { return (double)GetValue(StrokeThicknessProperty); }
            set { SetValue(StrokeThicknessProperty, value); }
        }

        public Brush ProgressBrush
        {
            get { return (Brush)GetValue(ProgressBrushProperty); }
            set { SetValue(ProgressBrushProperty, value); }
        }

        public Brush TrackBrush
        {
            get { return (Brush)GetValue(TrackBrushProperty); }
            set { SetValue(TrackBrushProperty, value); }
        }

        static CircularProgressBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CircularProgressBar), new FrameworkPropertyMetadata(typeof(CircularProgressBar)));
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as CircularProgressBar;
            if (control != null)
            {
                // Trigger animation or visual update if needed when value changes
            }
        }
    }
}
