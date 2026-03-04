using System.Windows;

namespace DailyDash.Views
{
    public static class FocusExtension
    {
        public static readonly DependencyProperty IsFocusedProperty =
            DependencyProperty.RegisterAttached(
                "IsFocused",
                typeof(bool),
                typeof(FocusExtension),
                new UIPropertyMetadata(false, OnIsFocusedPropertyChanged));

        public static bool GetIsFocused(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsFocusedProperty);
        }

        public static void SetIsFocused(DependencyObject obj, bool value)
        {
            obj.SetValue(IsFocusedProperty, value);
        }

        private static void OnIsFocusedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && (bool)e.NewValue)
            {
                element.Dispatcher.BeginInvoke(new System.Action(() => {
                    element.Focus();
                    if (element is System.Windows.Controls.TextBox tb)
                    {
                        tb.CaretIndex = tb.Text.Length;
                    }
                }));
            }
        }
    }
}
