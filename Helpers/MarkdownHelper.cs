using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System;
using System.IO;
using System.Windows.Media;

namespace DailyDash.Helpers
{
    public static class MarkdownHelper
    {
        public static readonly DependencyProperty MarkdownTextProperty =
            DependencyProperty.RegisterAttached(
                "MarkdownText",
                typeof(string),
                typeof(MarkdownHelper),
                new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

        public static string GetMarkdownText(DependencyObject obj)
        {
            return (string)obj.GetValue(MarkdownTextProperty);
        }

        public static void SetMarkdownText(DependencyObject obj, string value)
        {
            obj.SetValue(MarkdownTextProperty, value);
        }

        private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock)
            {
                textBlock.Inlines.Clear();
                string text = e.NewValue as string;

                if (string.IsNullOrEmpty(text))
                {
                    // Add a placeholder-like text to ensure the TextBlock has some height/clickable area
                    textBlock.Inlines.Add(new Run("Clique aqui para digitar suas anotações...") { Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), FontStyle = FontStyles.Italic });
                    return;
                }

                // Match basic markdown tokens: images, bold, italic, strikethrough, header, list
                var regex = new Regex(@"(!\[.*?\]\(.*?\)|\*\*.*?\*\*|\*.*?\*|~~.*?~~|### .*|^- .*)", RegexOptions.Multiline);
                int lastPos = 0;

                foreach (Match match in regex.Matches(text))
                {
                    if (match.Index > lastPos)
                    {
                        string plain = text.Substring(lastPos, match.Index - lastPos);
                        textBlock.Inlines.Add(new Run(plain));
                    }

                    string token = match.Value;
                    if (token.StartsWith("### "))
                    {
                        textBlock.Inlines.Add(new Run(token.Substring(4)) { FontSize = textBlock.FontSize * 1.5, FontWeight = FontWeights.Bold });
                    }
                    else if (token.StartsWith("- "))
                    {
                        textBlock.Inlines.Add(new Run("• " + token.Substring(2)) { FontWeight = FontWeights.SemiBold });
                    }
                    else if (token.StartsWith("**") && token.EndsWith("**"))
                    {
                        textBlock.Inlines.Add(new Run(token.Substring(2, token.Length - 4)) { FontWeight = FontWeights.Bold });
                    }
                    else if (token.StartsWith("*") && token.EndsWith("*") && token.Length > 2)
                    {
                        textBlock.Inlines.Add(new Run(token.Substring(1, token.Length - 2)) { FontStyle = FontStyles.Italic });
                    }
                    else if (token.StartsWith("~~") && token.EndsWith("~~"))
                    {
                        var run = new Run(token.Substring(2, token.Length - 4));
                        run.TextDecorations = TextDecorations.Strikethrough;
                        textBlock.Inlines.Add(run);
                    }
                    else if (token.StartsWith("![") && token.Contains("]("))
                    {
                        int altEnd = token.IndexOf("](");
                        int urlStart = altEnd + 2;
                        int urlEnd = token.Length - 1;
                        string url = token.Substring(urlStart, urlEnd - urlStart);
                        
                        try
                        {
                            string basePath = AppDomain.CurrentDomain.BaseDirectory;
                            string fullPath = url.StartsWith("/") ? Path.Combine(basePath, url.Substring(1).Replace("/", "\\")) : Path.Combine(basePath, url.Replace("/", "\\"));
                            
                            if (File.Exists(fullPath))
                            {
                                var bmp = new BitmapImage();
                                bmp.BeginInit();
                                bmp.CacheOption = BitmapCacheOption.OnLoad;
                                bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                                bmp.EndInit();

                                var img = new Image { Source = bmp, MaxWidth = 350, Stretch = Stretch.Uniform, Margin = new Thickness(0, 5, 0, 5) };
                                textBlock.Inlines.Add(new InlineUIContainer(img));
                            }
                            else
                            {
                                textBlock.Inlines.Add(new Run($"[Imagem não encontrada: {url}]") { Foreground = Brushes.Red });
                            }
                        }
                        catch
                        {
                            textBlock.Inlines.Add(new Run(token));
                        }
                    }

                    lastPos = match.Index + match.Length;
                }

                if (lastPos < text.Length)
                {
                    textBlock.Inlines.Add(new Run(text.Substring(lastPos)));
                }
            }
        }
    }
}
