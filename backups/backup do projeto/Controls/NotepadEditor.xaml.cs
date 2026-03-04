using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace DailyDash.Controls
{
    public partial class NotepadEditor : UserControl
    {
        private bool _isInternalUpdate = false;

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
                if (editor.EditorBox != null)
                {
                    editor.EditorBox.Focus();
                    editor.EditorBox.CaretPosition = editor.EditorBox.Document.ContentEnd;
                }
            }
        }

        public NotepadEditor()
        {
            InitializeComponent();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NotepadEditor editor && !editor._isInternalUpdate)
            {
                if (editor.EditorBox != null)
                {
                    var markdown = e.NewValue as string ?? string.Empty;
                    editor.LoadMarkdown(markdown);
                }
            }
        }

        private void LoadMarkdown(string markdown)
        {
            EditorBox.Document.Blocks.Clear();
            if (string.IsNullOrWhiteSpace(markdown)) { return; }

            // Split into paragraphs by double newlines, or process line by line.
            // Simplified markdown loader
            string[] lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            
            Paragraph currentParagraph = new Paragraph();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    if (currentParagraph.Inlines.Count > 0)
                    {
                        EditorBox.Document.Blocks.Add(currentParagraph);
                        currentParagraph = new Paragraph();
                    }
                    else
                    {
                        EditorBox.Document.Blocks.Add(new Paragraph(new Run("")));
                    }
                    continue;
                }

                bool isHeader = line.StartsWith("### ");
                bool isList = line.StartsWith("- ");
                
                string processLine = line;
                if (isHeader) processLine = line.Substring(4);
                if (isList) processLine = "• " + line.Substring(2);

                ParseInlines(processLine, currentParagraph, isHeader);

                if (i < lines.Length - 1)
                {
                    // If next line is not empty, soft break inside paragraph
                    if (!string.IsNullOrWhiteSpace(lines[i+1]))
                        currentParagraph.Inlines.Add(new LineBreak());
                }
            }

            if (currentParagraph.Inlines.Count > 0)
            {
                EditorBox.Document.Blocks.Add(currentParagraph);
            }
        }

        private void ParseInlines(string text, Paragraph p, bool isHeader)
        {
            // Similar to MarkdownHelper regex
            var regex = new Regex(@"(!\[.*?\]\(.*?\)|\*\*.*?\*\*|\*.*?\*|~~.*?~~)", RegexOptions.Multiline);
            int lastPos = 0;

            if (isHeader)
            {
                p.FontSize = this.FontSize * 1.5;
                p.FontWeight = FontWeights.Bold;
            }

            foreach (Match match in regex.Matches(text))
            {
                if (match.Index > lastPos)
                {
                    p.Inlines.Add(new Run(text.Substring(lastPos, match.Index - lastPos)));
                }

                string token = match.Value;
                Run run = null;

                if (token.StartsWith("**"))
                {
                    run = new Run(token.Substring(2, token.Length - 4)) { FontWeight = FontWeights.Bold };
                }
                else if (token.StartsWith("*"))
                {
                    run = new Run(token.Substring(1, token.Length - 2)) { FontStyle = FontStyles.Italic };
                }
                else if (token.StartsWith("~~"))
                {
                    run = new Run(token.Substring(2, token.Length - 4));
                    run.TextDecorations = TextDecorations.Strikethrough;
                }

                if (run != null)
                {
                    p.Inlines.Add(run);
                }
                else if (token.StartsWith("!["))
                {
                    int altEnd = token.IndexOf("](");
                    string altArgs = token.Substring(2, altEnd - 2); // e.g. "size=300,align=left"
                    string url = token.Substring(altEnd + 2, token.Length - altEnd - 3);

                    double width = 350;
                    string align = "Center";

                    foreach (var arg in altArgs.Split(','))
                    {
                        var kv = arg.Split('=');
                        if(kv.Length == 2) {
                            if (kv[0].Trim() == "size" && double.TryParse(kv[1], out double sz)) width = sz;
                            if (kv[0].Trim() == "align") align = kv[1].Trim();
                        }
                    }

                    var widget = new ImageWidget();
                    widget.LoadImage(url, width, align);
                    
                    p.Inlines.Add(new InlineUIContainer(widget) { BaselineAlignment = BaselineAlignment.Center });
                }

                lastPos = match.Index + match.Length;
            }

            if (lastPos < text.Length)
            {
                p.Inlines.Add(new Run(text.Substring(lastPos)));
            }
        }

        public void SaveMarkdown()
        {
            StringBuilder sb = new StringBuilder();
            
            foreach (var block in EditorBox.Document.Blocks)
            {
                if (block is Paragraph p)
                {
                    bool isHeader = p.FontSize > this.FontSize;
                    if (isHeader) sb.Append("### ");

                    foreach (var inline in p.Inlines)
                    {
                        if (inline is Run run)
                        {
                            string text = run.Text;
                            if (string.IsNullOrEmpty(text)) continue;

                            // Inverse list bullet processing
                            if (text.StartsWith("• ") && run == p.Inlines.FirstInline) {
                                sb.Append("- ");
                                text = text.Substring(2);
                            }

                            if (run.FontWeight == FontWeights.Bold) text = "**" + text + "**";
                            if (run.FontStyle == FontStyles.Italic) text = "*" + text + "*";
                            
                            if (run.TextDecorations != null && run.TextDecorations.Count > 0)
                            {
                                text = "~~" + text + "~~";
                            }

                            sb.Append(text);
                        }
                        else if (inline is InlineUIContainer ui && ui.Child is ImageWidget widget)
                        {
                            sb.Append($"![size={widget.MainImage.Width},align={widget.GetMarkdownAlignment()}]({widget.ImageUrl})");
                        }
                        else if (inline is LineBreak)
                        {
                            sb.AppendLine();
                            if(isHeader) sb.Append("### ");
                        }
                    }
                    sb.AppendLine();
                }
            }

            _isInternalUpdate = true;
            Text = sb.ToString().TrimEnd();
            _isInternalUpdate = false;
        }

        private void EditorBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveMarkdown();
            IsFocused = false;
        }

        private void EditorBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void EditorBox_Drop(object sender, DragEventArgs e)
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

                            string url = $"/data/assets/{fileName}";
                            var widget = new ImageWidget();
                            widget.LoadImage(url, 350, "Center");

                            var p = EditorBox.CaretPosition.Paragraph;
                            if (p == null) {
                                p = new Paragraph();
                                EditorBox.Document.Blocks.Add(p);
                            }
                            
                            EditorBox.CaretPosition.InsertTextInRun("");
                            var ui = new InlineUIContainer(widget, EditorBox.CaretPosition);
                            EditorBox.CaretPosition = ui.ElementEnd;

                            SaveMarkdown();
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
