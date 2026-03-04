using System.Windows;
using DailyDash.ViewModels;

namespace DailyDash.Views
{
    public partial class InsightsView : Window
    {
        public InsightsView()
        {
            InitializeComponent();
            var vm = new InsightsViewModel();
            DataContext = vm;
            Loaded += async (s, e) => await vm.LoadInsightsAsync();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
