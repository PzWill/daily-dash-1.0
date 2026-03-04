using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media.Imaging;

namespace DailyDash.Controls
{
    public partial class ImageWidget : UserControl
    {
        public string ImageUrl { get; set; } = string.Empty;

        public ImageWidget()
        {
            InitializeComponent();
        }

        public void LoadImage(string markdownUrl, double initialWidth, string alignment)
        {
            ImageUrl = markdownUrl;
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string fullPath = markdownUrl.StartsWith("/") 
                    ? Path.Combine(basePath, markdownUrl.Substring(1).Replace("/", "\\")) 
                    : Path.Combine(basePath, markdownUrl.Replace("/", "\\"));

                if (File.Exists(fullPath))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bmp.EndInit();
                    MainImage.Source = bmp;
                }
            }
            catch { }

            MainImage.Width = initialWidth > 0 ? initialWidth : 350;
            SizeSlider.Value = MainImage.Width;

            ApplyAlignment(alignment);
        }

        private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            ControlsOverlay.Visibility = Visibility.Visible;
        }

        private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            ControlsOverlay.Visibility = Visibility.Collapsed;
        }

        private void SizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MainImage != null)
            {
                MainImage.Width = e.NewValue;
            }
        }

        private void AlignLeft_Click(object sender, RoutedEventArgs e) => ApplyAlignment("Left");
        private void AlignCenter_Click(object sender, RoutedEventArgs e) => ApplyAlignment("Center");
        private void AlignRight_Click(object sender, RoutedEventArgs e) => ApplyAlignment("Right");

        private void ApplyAlignment(string align)
        {
            if (this.Parent is InlineUIContainer container)
            {
                // Find parent paragraph
                if (container.Parent is Paragraph p)
                {
                    switch (align?.ToLower())
                    {
                        case "left":
                            p.TextAlignment = TextAlignment.Left;
                            break;
                        case "right":
                            p.TextAlignment = TextAlignment.Right;
                            break;
                        default:
                            p.TextAlignment = TextAlignment.Center;
                            break;
                    }
                }
            }
        }

        public string GetMarkdownAlignment()
        {
            if (this.Parent is InlineUIContainer container && container.Parent is Paragraph p)
            {
                if (p.TextAlignment == TextAlignment.Left) return "Left";
                if (p.TextAlignment == TextAlignment.Right) return "Right";
            }
            return "Center";
        }
    }
}
