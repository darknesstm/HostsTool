using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Editing;

namespace HostsTool;

public class HostsSyntaxTextBox : System.Windows.Controls.UserControl
{
    private readonly TextEditor _editor;
    private bool _isUpdating;

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(HostsSyntaxTextBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextPropertyChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly),
        typeof(bool),
        typeof(HostsSyntaxTextBox),
        new PropertyMetadata(false, OnIsReadOnlyChanged));

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public HostsSyntaxTextBox()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = true,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 13,
            SyntaxHighlighting = null,
            IsReadOnly = false
        };

        // Attach colorizer for hosts format
        _editor.TextArea.TextView.LineTransformers.Add(new HostsColorizer());

        // Keep the AvalonEdit text and our dependency property in sync
        _editor.TextChanged += Editor_TextChanged;

        Content = _editor;
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_isUpdating) return;
        try
        {
            _isUpdating = true;
            if (Text != _editor.Text)
                SetCurrentValue(TextProperty, _editor.Text);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private static void OnTextPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HostsSyntaxTextBox ctrl && !ctrl._isUpdating)
        {
            try
            {
                ctrl._isUpdating = true;
                var newText = e.NewValue as string ?? string.Empty;
                if (ctrl._editor.Text != newText)
                    ctrl._editor.Text = newText;
            }
            finally
            {
                ctrl._isUpdating = false;
            }
        }
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HostsSyntaxTextBox ctrl)
        {
            ctrl._editor.IsReadOnly = (bool)e.NewValue;
        }
    }

    // Simple colorizer: IP (steel blue), hostnames (dark green-ish), comments (gray)
    private class HostsColorizer : DocumentColorizingTransformer
    {
        private static readonly Regex HostLineRegex = new Regex(@"^\s*(?<ip>(?:\d{1,3}\.){3}\d{1,3}|(?:[0-9a-fA-F]{0,4}:){2,7}[0-9a-fA-F]{0,4})(?<rest>.*)$", RegexOptions.Compiled);

        protected override void ColorizeLine(DocumentLine line)
        {
            var doc = CurrentContext.Document;
            var text = doc.GetText(line);
            if (string.IsNullOrEmpty(text))
                return;

            int lineStart = line.Offset;

            // Full-line comment
            var trimmed = text.TrimStart();
            if (trimmed.StartsWith("#"))
            {
                ChangeLinePart(lineStart + text.IndexOf('#'), lineStart + text.Length, r => r.TextRunProperties.SetForegroundBrush(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x99, 0x55))));
                return;
            }

            var m = HostLineRegex.Match(text);
            if (m.Success)
            {
                var ipGroup = m.Groups["ip"];
                var restGroup = m.Groups["rest"];

                if (ipGroup.Success)
                {
                    int s = lineStart + ipGroup.Index;
                    int e = s + ipGroup.Length;
                    ChangeLinePart(s, e, r => r.TextRunProperties.SetForegroundBrush(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4F, 0xC1, 0xFF))));
                }

                if (restGroup.Success)
                {
                    var rest = restGroup.Value;
                    var commentIndex = rest.IndexOf('#');
                    if (commentIndex >= 0)
                    {
                        // hostnames before comment
                        if (commentIndex > 0)
                        {
                            var before = rest.Substring(0, commentIndex);
                            ColorizeHostNames(lineStart + restGroup.Index, before);
                        }

                        // comment
                        int cs = lineStart + restGroup.Index + commentIndex;
                        ChangeLinePart(cs, lineStart + text.Length, r => r.TextRunProperties.SetForegroundBrush(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x99, 0x55))));
                    }
                    else
                    {
                        ColorizeHostNames(lineStart + restGroup.Index, rest);
                    }
                }
            }
            else
            {
                // If no IP matched, still try to colorize comments inside the line
                var idx = text.IndexOf('#');
                if (idx >= 0)
                {
                    ChangeLinePart(lineStart + idx, lineStart + text.Length, r => r.TextRunProperties.SetForegroundBrush(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x99, 0x55))));
                }
            }
        }

        private void ColorizeHostNames(int absoluteStart, string text)
        {
            // Split by whitespace and color tokens that look like hostnames
            var parts = Regex.Split(text, "(\\s+)");
            int offset = 0;
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    offset += part.Length;
                    continue;
                }

                if (Regex.IsMatch(part, "^\\s+$"))
                {
                    offset += part.Length;
                    continue;
                }

                if (Regex.IsMatch(part, "^(?:[a-zA-Z0-9_-]+\\.)*[a-zA-Z0-9_-]+$"))
                {
                    int s = absoluteStart + offset;
                    int e = s + part.Length;
                    ChangeLinePart(s, e, r => r.TextRunProperties.SetForegroundBrush(new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xCE, 0x91, 0x78))));
                }

                offset += part.Length;
            }
        }
    }
}
