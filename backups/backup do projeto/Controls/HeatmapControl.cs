using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DailyDash.Controls
{
    public class HeatmapControl : Control
    {
        private struct CellInfo
        {
            public Rect Bounds;
            public DateTime Date;
            public int Count;
        }

        private List<CellInfo> _cells = new List<CellInfo>();

        public HeatmapControl()
        {
            Background = Brushes.Transparent; // Important for HitTesting
        }

        public static readonly DependencyProperty ContributionDataProperty =
            DependencyProperty.Register(
                nameof(ContributionData),
                typeof(Dictionary<DateTime, int>),
                typeof(HeatmapControl),
                new FrameworkPropertyMetadata(new Dictionary<DateTime, int>(), FrameworkPropertyMetadataOptions.AffectsRender, OnDataChanged));

        public Dictionary<DateTime, int> ContributionData
        {
            get => (Dictionary<DateTime, int>)GetValue(ContributionDataProperty);
            set => SetValue(ContributionDataProperty, value);
        }

        public static readonly DependencyProperty CellSizeProperty =
            DependencyProperty.Register(nameof(CellSize), typeof(double), typeof(HeatmapControl), new PropertyMetadata(12.0, OnVisualChanged));

        public double CellSize
        {
            get => (double)GetValue(CellSizeProperty);
            set => SetValue(CellSizeProperty, value);
        }

        public static readonly DependencyProperty CellSpacingProperty =
            DependencyProperty.Register(nameof(CellSpacing), typeof(double), typeof(HeatmapControl), new PropertyMetadata(4.0, OnVisualChanged));

        public double CellSpacing
        {
            get => (double)GetValue(CellSpacingProperty);
            set => SetValue(CellSpacingProperty, value);
        }

        public static readonly DependencyProperty EmptyCellBrushProperty =
            DependencyProperty.Register(nameof(EmptyCellBrush), typeof(Brush), typeof(HeatmapControl), new PropertyMetadata(new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), OnVisualChanged));

        public Brush EmptyCellBrush
        {
            get => (Brush)GetValue(EmptyCellBrushProperty);
            set => SetValue(EmptyCellBrushProperty, value);
        }

        public static readonly DependencyProperty BaseColorProperty =
            DependencyProperty.Register(nameof(BaseColor), typeof(Color), typeof(HeatmapControl), new PropertyMetadata(Colors.DeepSkyBlue, OnVisualChanged));

        public Color BaseColor
        {
            get => (Color)GetValue(BaseColorProperty);
            set => SetValue(BaseColorProperty, value);
        }

        private static void OnDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeatmapControl control)
            {
                control.InvalidateVisual();
            }
        }

        private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HeatmapControl control)
            {
                control.InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            
            // Draw a subtle background for hit testing all over
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(new Point(0, 0), RenderSize));

            _cells.Clear();

            if (ContributionData == null) return;

            int daysInWeek = 7;
            int weeksToDisplay = 2; // Precisely 2 weeks as requested (14 days)

            DateTime endDate = DateTime.Today;
            
            // The last column represents the current week (Sunday to Saturday)
            // So the end of that week is the upcoming Saturday
            int daysUntilSaturday = (int)DayOfWeek.Saturday - (int)endDate.DayOfWeek;
            DateTime endOfWeek = endDate.AddDays(daysUntilSaturday);

            DateTime startDate = endOfWeek.AddDays(-(weeksToDisplay * daysInWeek - 1));

            int maxContributions = 1;
            if (ContributionData.Count > 0)
            {
                // Find maximum on the last 14 days specifically, or overall
                var recentData = ContributionData.Where(kvp => kvp.Key >= startDate && kvp.Key <= endDate).ToList();
                if (recentData.Count > 0)
                {
                    maxContributions = recentData.Max(x => x.Value);
                }
                if (maxContributions == 0) maxContributions = 1; // Prevent division by zero
            }

            double xOffset = 0;
            double yOffset = 0;

            DateTime currentDate = startDate;

            for (int row = 0; row < weeksToDisplay; row++)
            {
                xOffset = 0;
                for (int col = 0; col < daysInWeek; col++)
                {
                    if (currentDate <= endDate)
                    {
                        int count = ContributionData.ContainsKey(currentDate.Date) ? ContributionData[currentDate.Date] : 0;
                        Brush cellBrush = GetBrushForCount(count, maxContributions);

                        Rect rect = new Rect(xOffset, yOffset, CellSize, CellSize);
                        drawingContext.DrawRoundedRectangle(cellBrush, null, rect, 2, 2); // Corner radius of 2
                        
                        _cells.Add(new CellInfo { Bounds = rect, Date = currentDate, Count = count });
                    }

                    xOffset += CellSize + CellSpacing;
                    currentDate = currentDate.AddDays(1);
                }
                yOffset += CellSize + CellSpacing;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);
            foreach (var cell in _cells)
            {
                // Give a little padding for easier hovering
                var hitRect = cell.Bounds;
                hitRect.Inflate(CellSpacing / 2, CellSpacing / 2);

                if (hitRect.Contains(p))
                {
                    string tasksStr = cell.Count == 1 ? "tarefa realizada" : "tarefas realizadas";
                    this.ToolTip = $"{cell.Date:dd/MM/yyyy}\n{cell.Count} {tasksStr}";
                    return;
                }
            }
            this.ToolTip = null;
        }

        private Brush GetBrushForCount(int count, int max)
        {
            if (count == 0 || max == 0) return EmptyCellBrush;

            double intensity = (double)count / max;
            // Clamp intensity to make sure even 1 task is visible, and it scales nicely
            if (intensity < 0.3) intensity = 0.3; 
            if (intensity > 1.0) intensity = 1.0;

            Color c = BaseColor;
            // Adjust alpha based on intensity
            Color adjustedColor = Color.FromArgb((byte)(intensity * 255), c.R, c.G, c.B);
            
            SolidColorBrush brush = new SolidColorBrush(adjustedColor);
            brush.Freeze(); // Optimize
            return brush;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int daysInWeek = 7;
            int weeksToDisplay = 2; // Precisely 2 weeks

            double width = (daysInWeek * (CellSize + CellSpacing)) - CellSpacing;
            double height = (weeksToDisplay * (CellSize + CellSpacing)) - CellSpacing;

            return new Size(Math.Max(0, width), Math.Max(0, height));
        }
    }
}
