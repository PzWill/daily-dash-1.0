using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DailyDash.Services;
using DailyDash.ViewModels;

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
    }
}
