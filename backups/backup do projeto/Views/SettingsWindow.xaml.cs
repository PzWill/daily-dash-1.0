using System.Windows;
using DailyDash.Services;

namespace DailyDash.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void LoadCurrentSettings()
        {
            OpacitySlider.Value = SettingsManager.CurrentSettings.PanelOpacity;
            RadiusSlider.Value = SettingsManager.CurrentSettings.GlobalCornerRadius;
            ColorTextBox.Text = SettingsManager.CurrentSettings.PrimaryColorHex;
            FontSizeSlider.Value = SettingsManager.CurrentSettings.AppFontSize;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.CurrentSettings.PanelOpacity = OpacitySlider.Value;
            SettingsManager.CurrentSettings.GlobalCornerRadius = RadiusSlider.Value;
            SettingsManager.CurrentSettings.PrimaryColorHex = ColorTextBox.Text;
            SettingsManager.CurrentSettings.AppFontSize = FontSizeSlider.Value;
            
            SettingsManager.SaveSettings();
            SettingsManager.ApplyCurrentSettings();
            
            MessageBox.Show("Configurações salvas!", "Sucesso", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
    }
}
