using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DailyDash.Views
{
    /// <summary>
    /// Provides watermark/placeholder text support for TextBox controls via an adorner-style approach.
    /// The placeholder is shown as a TextBlock behind the TextBox when the text is empty.
    /// Usage: local:PlaceholderHelper.Placeholder="Your placeholder..."
    /// </summary>
    public static class PlaceholderHelper
    {
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.RegisterAttached(
                "Placeholder",
                typeof(string),
                typeof(PlaceholderHelper),
                new PropertyMetadata(string.Empty, OnPlaceholderChanged));

        public static string GetPlaceholder(DependencyObject obj) =>
            (string)obj.GetValue(PlaceholderProperty);

        public static void SetPlaceholder(DependencyObject obj, string value) =>
            obj.SetValue(PlaceholderProperty, value);

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                textBox.Loaded -= TextBox_Loaded;
                
                if (!string.IsNullOrEmpty((string)e.NewValue))
                {
                    textBox.Loaded += TextBox_Loaded;
                    
                    // If already loaded, apply immediately
                    if (textBox.IsLoaded)
                    {
                        SetupPlaceholder(textBox);
                    }
                }
            }
        }

        private static void TextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                SetupPlaceholder(tb);
            }
        }

        private static void SetupPlaceholder(TextBox textBox)
        {
            // Find or create the placeholder tag
            var tag = textBox.Tag as TextBlock;
            
            // We use a different approach: style-based placeholder
            // Set the textbox background when empty
            UpdatePlaceholderVisual(textBox);
            
            textBox.TextChanged -= OnTextChanged;
            textBox.TextChanged += OnTextChanged;
            textBox.GotFocus -= OnGotFocus;
            textBox.GotFocus += OnGotFocus;
            textBox.LostFocus -= OnLostFocus;
            textBox.LostFocus += OnLostFocus;
        }

        private static void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
                UpdatePlaceholderVisual(tb);
        }

        private static void OnGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
                UpdatePlaceholderVisual(tb);
        }

        private static void OnLostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
                UpdatePlaceholderVisual(tb);
        }

        private static void UpdatePlaceholderVisual(TextBox tb)
        {
            var placeholder = GetPlaceholder(tb);
            if (string.IsNullOrEmpty(tb.Text) && !tb.IsFocused)
            {
                // Show placeholder via a visual brush background
                var label = new TextBlock
                {
                    Text = placeholder,
                    Foreground = new SolidColorBrush(Color.FromArgb(0x66, 0xAA, 0xAA, 0xAA)),
                    FontStyle = FontStyles.Italic,
                    FontSize = tb.FontSize,
                    Margin = new Thickness(4, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var brush = new VisualBrush(label)
                {
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Center,
                    Stretch = Stretch.None
                };

                tb.Background = brush;
            }
            else
            {
                tb.Background = Brushes.Transparent;
            }
        }
    }
}
