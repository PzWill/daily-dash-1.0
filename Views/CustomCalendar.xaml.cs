using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace DailyDash.Views
{
    public partial class CustomCalendar : UserControl
    {
        // The currently displayed month/year
        private DateTime _displayDate;

        // Dependency Property for the selected date (two-way bindable)
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(
                nameof(SelectedDate),
                typeof(DateTime),
                typeof(CustomCalendar),
                new FrameworkPropertyMetadata(
                    DateTime.Today,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedDateChanged));

        public DateTime SelectedDate
        {
            get => (DateTime)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        public static readonly DependencyProperty DeadlinesProperty =
            DependencyProperty.Register(
                nameof(Deadlines),
                typeof(System.Collections.IEnumerable),
                typeof(CustomCalendar),
                new FrameworkPropertyMetadata(null, OnDeadlinesChanged));

        public System.Collections.IEnumerable Deadlines
        {
            get => (System.Collections.IEnumerable)GetValue(DeadlinesProperty);
            set => SetValue(DeadlinesProperty, value);
        }

        private static void OnDeadlinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomCalendar cal)
            {
                cal.BuildCalendar();
            }
        }

        private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomCalendar cal)
            {
                var newDate = (DateTime)e.NewValue;
                // Jump display to the selected month if different
                if (cal._displayDate.Year != newDate.Year || cal._displayDate.Month != newDate.Month)
                {
                    cal._displayDate = new DateTime(newDate.Year, newDate.Month, 1);
                }
                cal.BuildCalendar();
            }
        }

        public CustomCalendar()
        {
            InitializeComponent();
            _displayDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            BuildDayOfWeekHeaders();
            BuildCalendar();
        }

        private void BuildDayOfWeekHeaders()
        {
            DayOfWeekRow.Children.Clear();
            // Portuguese abbreviated day names
            string[] dayNames = { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };
            foreach (var name in dayNames)
            {
                var tb = new TextBlock
                {
                    Text = name,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                DayOfWeekRow.Children.Add(tb);
            }
        }

        private void BuildCalendar()
        {
            DaysGrid.Children.Clear();

            // Update header text
            var culture = new CultureInfo("pt-BR");
            string monthName = _displayDate.ToString("MMMM", culture);
            // Capitalize first letter
            monthName = char.ToUpper(monthName[0]) + monthName.Substring(1);
            MonthYearText.Text = $"{monthName} {_displayDate.Year}";

            // First day of the month
            var firstOfMonth = new DateTime(_displayDate.Year, _displayDate.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(_displayDate.Year, _displayDate.Month);

            // Day of week offset (Sunday = 0)
            int startDayOfWeek = (int)firstOfMonth.DayOfWeek;

            // Previous month fill
            DateTime prevMonth = firstOfMonth.AddMonths(-1);
            int daysInPrevMonth = DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month);

            // Fill 42 cells (6 rows x 7 cols)
            for (int i = 0; i < 42; i++)
            {
                int dayNumber;
                bool isCurrentMonth;
                DateTime cellDate;

                if (i < startDayOfWeek)
                {
                    // Previous month days
                    dayNumber = daysInPrevMonth - startDayOfWeek + i + 1;
                    isCurrentMonth = false;
                    cellDate = new DateTime(prevMonth.Year, prevMonth.Month, dayNumber);
                }
                else if (i >= startDayOfWeek + daysInMonth)
                {
                    // Next month days
                    dayNumber = i - startDayOfWeek - daysInMonth + 1;
                    isCurrentMonth = false;
                    DateTime nextMonth = firstOfMonth.AddMonths(1);
                    cellDate = new DateTime(nextMonth.Year, nextMonth.Month, dayNumber);
                }
                else
                {
                    // Current month days
                    dayNumber = i - startDayOfWeek + 1;
                    isCurrentMonth = true;
                    cellDate = new DateTime(_displayDate.Year, _displayDate.Month, dayNumber);
                }

                var btn = CreateDayButton(dayNumber, cellDate, isCurrentMonth);
                DaysGrid.Children.Add(btn);
            }

            UpdateSelectedDayDeadlines();
        }

        private void UpdateSelectedDayDeadlines()
        {
            var items = new System.Collections.Generic.List<object>();
            if (Deadlines != null)
            {
                foreach (var item in Deadlines)
                {
                    if (item is Models.TaskItem task && task.Deadline.HasValue && task.Deadline.Value.Date == SelectedDate.Date)
                    {
                        items.Add(new { 
                            Title = task.Title, 
                            TagColorBrush = GetBrushFromHex(task.TagColor)
                        });
                    }
                }
            }
            if (items.Count == 0) items.Add(new { Title = "Nenhum prazo para hoje.", TagColorBrush = Brushes.Transparent });
            SelectedDayDeadlinesList.ItemsSource = items;
        }

        private Brush GetBrushFromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex == "Transparent") 
                return new SolidColorBrush(Color.FromArgb(255, 142, 142, 147)); // iOS Gray (SystemGray)
            try
            {
                return (Brush)new BrushConverter().ConvertFromString(hex);
            }
            catch
            {
                return new SolidColorBrush(Color.FromArgb(255, 255, 69, 58)); // Red default
            }
        }

        private Button CreateDayButton(int dayNumber, DateTime cellDate, bool isCurrentMonth)
        {
            bool isToday = cellDate.Date == DateTime.Today;
            bool isSelected = cellDate.Date == SelectedDate.Date;

            // Determine background
            Brush bg = Brushes.Transparent;
            if (isSelected)
                bg = new SolidColorBrush(Color.FromArgb(0x90, 0x00, 0x78, 0xD7)); // blue highlight
            else if (isToday)
                bg = new SolidColorBrush(Color.FromArgb(0x50, 0x00, 0x78, 0xD7)); // subtle blue

            // Determine foreground
            Brush fg;
            if (!isCurrentMonth)
                fg = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)); // dim white
            else
                fg = Brushes.White;

            // Check for deadlines
            var dayDeadlines = new System.Collections.Generic.List<Models.TaskItem>();
            string deadlineTitles = "";
            if (Deadlines != null)
            {
                foreach (var item in Deadlines.OfType<Models.TaskItem>())
                {
                    if (item.Deadline.HasValue && item.Deadline.Value.Date == cellDate.Date)
                    {
                        dayDeadlines.Add(item);
                        deadlineTitles += $"• {item.Title}\n";
                    }
                }
            }

            var grid = new Grid();
            grid.Children.Add(new TextBlock { 
                Text = dayNumber.ToString(), 
                HorizontalAlignment = HorizontalAlignment.Center, 
                VerticalAlignment = VerticalAlignment.Center 
            });
            
            if (dayDeadlines.Count > 0)
            {
                var dotsPanel = new StackPanel { 
                    Orientation = Orientation.Horizontal, 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0,0,0,4)
                };

                // Show up to 4 colored dots representing different task types/tags
                // Use distinct colors to avoid redundant dots of the same color
                var distinctColors = dayDeadlines
                    .Select(d => d.TagColor)
                    .Distinct()
                    .Take(4)
                    .ToList();

                foreach (var hex in distinctColors)
                {
                    dotsPanel.Children.Add(new Border { 
                        Background = GetBrushFromHex(hex),
                        Width = 4, Height = 4, 
                        CornerRadius = new CornerRadius(2), 
                        Margin = new Thickness(1, 0, 1, 0)
                    });
                }
                grid.Children.Add(dotsPanel);
            }

            var btn = new Button
            {
                Content = grid,
                Foreground = fg,
                Background = bg,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = isToday ? FontWeights.Bold : FontWeights.Normal,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                MinHeight = 28,
                MinWidth = 28,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Tag = cellDate
            };

            if (dayDeadlines.Count > 0)
            {
                btn.ToolTip = "Prazos Neste Dia:\n" + deadlineTitles.TrimEnd();
            }

            // Style the button template to have rounded corners and hover effect
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(2));

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;

            // Hover trigger
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))));
            template.Triggers.Add(hoverTrigger);

            btn.Template = template;
            btn.Click += DayButton_Click;

            return btn;
        }

        private void DayButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DateTime date)
            {
                SelectedDate = date;
                // If clicked a day from another month, navigate there
                if (date.Year != _displayDate.Year || date.Month != _displayDate.Month)
                {
                    _displayDate = new DateTime(date.Year, date.Month, 1);
                }
                BuildCalendar(); // This will also call UpdateSelectedDayDeadlines()
            }
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _displayDate = _displayDate.AddMonths(-1);
            BuildCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            _displayDate = _displayDate.AddMonths(1);
            BuildCalendar();
        }
    }
}
