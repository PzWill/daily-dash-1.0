using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace DailyDash.Services
{
    public static class WindowBlurHelper
    {
        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        // Windows 11 Windows Attributes
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        // Backdrop Types
        private const int DWMSBT_AUTO = 0;
        private const int DWMSBT_DISABLE = 1;
        private const int DWMSBT_MAINWINDOW = 2;   // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
        private const int DWMSBT_TABBEDWINDOW = 4; // Tabbed

        public static void EnableBlur(Window window, IntPtr hwnd)
        {
            try
            {
                // Enable Dark Mode for the Title bar and backdrop
                int trueValue = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref trueValue, Marshal.SizeOf(typeof(int)));

                // Try to set Acrylic Backdrop
                int backdropType = DWMSBT_TRANSIENTWINDOW; // Acrylic
                int result = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf(typeof(int)));

                // If Acrylic fails, fallback to Mica
                if (result != 0)
                {
                    backdropType = DWMSBT_MAINWINDOW; // Mica
                    DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, Marshal.SizeOf(typeof(int)));
                }
            }
            catch
            {
                // Ignoring errors for older Windows versions (Win 10) which might not support DWMWA_SYSTEMBACKDROP_TYPE
                // For full Windows 10 support, SetWindowCompositionAttribute (undocumented API) would be used, 
                // but Win11 DWM attributes are standard now.
            }
        }
    }
}
