using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace DailyDash.Controls
{
    public partial class NotepadEditor : UserControl
    {
        private bool _isInternalUpdate = false;
        private bool _isWebViewReady = false;
        private string _pendingText = string.Empty;

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                "Text",
                typeof(string),
                typeof(NotepadEditor),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static new readonly DependencyProperty IsFocusedProperty =
            DependencyProperty.Register(
                "IsFocused",
                typeof(bool),
                typeof(NotepadEditor),
                new FrameworkPropertyMetadata(false, OnIsFocusedChanged));

        public new bool IsFocused
        {
            get => (bool)GetValue(IsFocusedProperty);
            set => SetValue(IsFocusedProperty, value);
        }

        private static void OnIsFocusedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NotepadEditor editor && (bool)e.NewValue)
            {
                if (editor.EditorWebView != null && editor.EditorWebView.CoreWebView2 != null)
                {
                    editor.EditorWebView.Focus();
                }
            }
        }

        public NotepadEditor()
        {
            InitializeComponent();
            
            // Allow drag and drop on the control
            AllowDrop = true;
            PreviewDragOver += Editor_PreviewDragOver;
            Drop += Editor_Drop;
            
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            var userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyDash", "WebView2");
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await EditorWebView.EnsureCoreWebView2Async(env);
            
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            EditorWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("app.local", rootPath, CoreWebView2HostResourceAccessKind.Allow);
            
            EditorWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            EditorWebView.Source = new Uri("http://app.local/data/editor/index.html");
            
            // Disable default context menu if desired
            EditorWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            
            // Inject a style to keep scrollbars hidden/thin (already in CSS, but this covers webview natively)
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try 
            {
                string msg = e.WebMessageAsJson;
                if (string.IsNullOrEmpty(msg)) return;
                
                using (JsonDocument doc = JsonDocument.Parse(msg))
                {
                    var root = doc.RootElement;
                    string type = root.GetProperty("type").GetString() ?? "";
                    
                    if (type == "ready")
                    {
                        _isWebViewReady = true;
                        string initialMarkdown = _pendingText ?? Text ?? string.Empty;
                        _pendingText = string.Empty;
                        
                        var response = new 
                        { 
                            type = "load", 
                            data = initialMarkdown 
                        };
                        
                        EditorWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(response));
                    }
                    else if (type == "save" || type == "textChanged") 
                    {
                        if (!_isInternalUpdate)
                        {
                            string propertyName = type == "save" ? "data" : "text";
                            string safeText = root.GetProperty(propertyName).GetString() ?? "";
                            _isInternalUpdate = true;
                            Text = safeText.TrimEnd();
                            _isInternalUpdate = false;
                        }
                    }
                    else if (type == "uploadImage")
                    {
                        try
                        {
                            string id = root.GetProperty("id").GetString() ?? "";
                            string ext = root.GetProperty("extension").GetString() ?? "png";
                            string base64 = root.GetProperty("base64").GetString() ?? "";
                            
                            // Define assets dir
                            string assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "assets");
                            if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);
                            
                            // Generate file name
                            string fileName = $"img_{Guid.NewGuid():N}.{ext}";
                            string destPath = Path.Combine(assetsDir, fileName);
                            
                            // Save file
                            byte[] imageBytes = Convert.FromBase64String(base64);
                            File.WriteAllBytes(destPath, imageBytes);
                            
                            // Return the relative URL perfectly matching Obsidian
                            string relativeUrl = $"../assets/{fileName}";
                            
                            var response = new
                            {
                                type = "uploadResponse",
                                id = id,
                                url = relativeUrl
                            };
                            EditorWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(response));
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NotepadEditor editor && !editor._isInternalUpdate)
            {
                var markdown = e.NewValue as string ?? string.Empty;
                if (!editor._isWebViewReady)
                {
                    editor._pendingText = markdown;
                    return;
                }
                
                editor.UpdateEditorText(markdown);
            }
        }

        private async void UpdateEditorText(string markdown)
        {
            if (!_isWebViewReady || EditorWebView.CoreWebView2 == null) return;

            _isInternalUpdate = true;
            try 
            {
                string jsonString = System.Text.Json.JsonSerializer.Serialize(markdown);
                await EditorWebView.ExecuteScriptAsync($"if (window.setMarkdown) window.setMarkdown({jsonString});");
            }
            finally 
            {
                _isInternalUpdate = false;
            }
        }

        public void SaveMarkdown()
        {
            // Since milkdown automatically notifies via web messages, 
            // SaveMarkdown logic is mostly handled when `textChanged` is received.
            // But we can keep the method to satisfy existing interfaces if any.
        }

        private void Editor_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private async void Editor_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    string extension = Path.GetExtension(filePath).ToLower();
                    
                    if (extension == ".png" || extension == ".jpg" || extension == ".jpeg" || extension == ".gif")
                    {
                        try
                        {
                            string assetsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "assets");
                            if (!Directory.Exists(assetsDir)) Directory.CreateDirectory(assetsDir);

                            string fileName = Path.GetFileName(filePath);
                            string destPath = Path.Combine(assetsDir, fileName);

                            if (filePath != destPath) File.Copy(filePath, destPath, true);

                            // Defaulting to relative path so it's fully compatible with Obsidian
                            string url = $"../assets/{fileName}"; 
                            string mdImage = $"\n![size=350,align=Center]({url})\n";
                            
                            // Let's simply append the image markdown for now or use the insert function
                            if (_isWebViewReady && EditorWebView.CoreWebView2 != null)
                            {
                                string safeImageText = System.Web.HttpUtility.JavaScriptStringEncode(mdImage);
                                // The simplest way is to fetch current text, append, and set back,
                                // or define an insert function in JS. Let's do string append on C# side:
                                string currentText = Text ?? "";
                                string newText = currentText + mdImage;
                                Text = newText;
                                UpdateEditorText(newText);
                            }
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
