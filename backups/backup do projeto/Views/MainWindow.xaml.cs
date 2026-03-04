using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.IO;
using System.Windows.Controls;
using DailyDash.Services;
using DailyDash.ViewModels;
using System.Windows.Documents;

namespace DailyDash.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Enable native blur (Acrylic/Mica)
            WindowBlurHelper.EnableBlur(this, new WindowInteropHelper(this).Handle);
            
            if (DataContext is MainViewModel vm)
            {
                _ = vm.LoadDataAsync();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Allow window dragging
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
        }

        private void InsightsButton_Click(object sender, RoutedEventArgs e)
        {
            var insightsWindow = new InsightsView();
            insightsWindow.Owner = this;
            insightsWindow.ShowDialog();
        }

        private void FormatText_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string format)
            {
                var rtb = Keyboard.FocusedElement as System.Windows.Controls.RichTextBox;
                if (rtb == null)
                {
                    // Fallback: look for the editor box in the visual tree if focus is lost
                    rtb = Helpers.FocusHelper.FindVisualChild<RichTextBox>(this);
                }

                if (rtb != null)
                {
                    if (format == "Bold")
                    {
                        var cur = rtb.Selection.GetPropertyValue(TextElement.FontWeightProperty);
                        bool isBold = cur != DependencyProperty.UnsetValue && cur is FontWeight fw && fw == FontWeights.Bold;
                        rtb.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, isBold ? FontWeights.Normal : FontWeights.Bold);
                    }
                    else if (format == "Italic")
                    {
                        var cur = rtb.Selection.GetPropertyValue(TextElement.FontStyleProperty);
                        bool isItalic = cur != DependencyProperty.UnsetValue && cur is FontStyle fs && fs == FontStyles.Italic;
                        rtb.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, isItalic ? FontStyles.Normal : FontStyles.Italic);
                    }
                    else if (format == "Strikethrough")
                    {
                        var cur = rtb.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
                        bool isStrikethrough = cur != DependencyProperty.UnsetValue && cur is TextDecorationCollection tdc && tdc.Count > 0;
                        rtb.Selection.ApplyPropertyValue(Inline.TextDecorationsProperty, isStrikethrough ? null : TextDecorations.Strikethrough);
                    }
                    else if (format == "Header")
                    {
                        var p = rtb.CaretPosition.Paragraph;
                        if (p != null)
                        {
                            if (p.FontSize > rtb.FontSize) // Simplified check for "is header"
                            {
                                p.FontSize = rtb.FontSize;
                                p.FontWeight = FontWeights.Normal;
                            }
                            else
                            {
                                p.FontSize = rtb.FontSize * 1.5;
                                p.FontWeight = FontWeights.Bold;
                            }
                        }
                    }
                    else if (format == "List")
                    {
                        var p = rtb.CaretPosition.Paragraph;
                        if (p != null)
                        {
                            var firstRun = p.Inlines.FirstInline as Run;
                            if (firstRun != null && firstRun.Text.StartsWith("• "))
                            {
                                firstRun.Text = firstRun.Text.Substring(2);
                            }
                            else
                            {
                                p.Inlines.InsertBefore(p.Inlines.FirstInline, new Run("• "));
                            }
                        }
                    }
                    
                    rtb.Focus();
                    // Sync the change to the Markdown backing property
                    var editor = Helpers.FocusHelper.FindVisualParent<Controls.NotepadEditor>(rtb);
                    if (editor != null)
                    {
                        editor.SaveMarkdown();
                    }
                }
            }
        }

        private void NoteTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }



        private void ClearDeadline_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DailyDash.Models.TaskItem goal)
            {
                goal.Deadline = null;
            }
        }
    }
}
