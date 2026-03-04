using System.Configuration;
using System.Data;
using System.Windows;

using DailyDash.Services;

namespace DailyDash
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Load and apply custom UI settings like Opacity, Blur intensity, and colors.
            SettingsManager.LoadAndApplySettings();
        }
    }
}
