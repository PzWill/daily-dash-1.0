using System;
using System.Globalization;
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

            var btn = new Button
            {
                Content = dayNumber.ToString(),
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
                BuildCalendar();
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
