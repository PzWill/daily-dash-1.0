using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;

namespace DailyDash.Services
{
    public class AppSettings
    {
        public double GlobalCornerRadius { get; set; } = 12.0;
        public double PanelOpacity { get; set; } = 0.6;
        public string PrimaryColorHex { get; set; } = "#0078D7";
        public double AppFontSize { get; set; } = 14.0;
    }

    public static class SettingsManager
    {
        // Settings are stored in data/configuracoes/
        private static readonly string BaseFolder   = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DailyDash", "data");
        private static readonly string ConfigFolder = Path.Combine(BaseFolder, "configuracoes");
        private static readonly string FilePath     = Path.Combine(ConfigFolder, "settings.json");

        public static AppSettings CurrentSettings { get; private set; } = new AppSettings();

        public static void LoadAndApplySettings()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    CurrentSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }

            ApplyCurrentSettings();
        }

        public static void ApplyCurrentSettings()
        {
            if (Application.Current != null)
            {
                // Ensure SolidColorBrush is created and frozen or updated dynamically
                Color color;
                try
                {
                    color = (Color)ColorConverter.ConvertFromString(CurrentSettings.PrimaryColorHex)!;
                }
                catch
                {
                    color = (Color)ColorConverter.ConvertFromString("#0078D7")!; // Default fallback
                    CurrentSettings.PrimaryColorHex = "#0078D7"; // Reset to valid color
                }
                
                var panelColor = Color.FromArgb((byte)(CurrentSettings.PanelOpacity * 255), 0, 0, 0); // Black with opacity

                // Update Application Resources dictionary
                Application.Current.Resources["PrimaryAccentColor"] = color;
                Application.Current.Resources["PrimaryAccentBrush"] = new SolidColorBrush(color);
                
                Application.Current.Resources["PanelBackgroundColor"] = panelColor;
                Application.Current.Resources["PanelBackgroundBrush"] = new SolidColorBrush(panelColor);
                
                Application.Current.Resources["GlobalCornerRadius"] = new CornerRadius(CurrentSettings.GlobalCornerRadius);
                Application.Current.Resources["AppFontSize"] = CurrentSettings.AppFontSize;
                
                double baseFs = CurrentSettings.AppFontSize;
                for (int i = 6; i <= 60; i++)
                {
                    Application.Current.Resources[$"FontSize_{i}"] = baseFs * (i / 14.0);
                }
            }
        }

        public static void SaveSettings()
        {
            try
            {
                if (!Directory.Exists(ConfigFolder)) Directory.CreateDirectory(ConfigFolder);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(CurrentSettings, options);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
