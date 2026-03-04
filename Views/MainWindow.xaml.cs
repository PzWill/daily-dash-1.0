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
