using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Shici
{
    internal static class UiSmoke
    {
        internal static void Render()
        {
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            FrameworkElement view;
            using (var input = File.OpenRead(Path.Combine(root, "ui", "Main.xaml"))) view = (FrameworkElement)XamlReader.Load(input);
            view.SetValue(TextElement.FontFamilyProperty, new FontFamily("Microsoft YaHei UI"));
            view.SetValue(TextElement.FontSizeProperty, 13.0);
            WindowLayout.Apply(view, false);
            Save(view, 750, 635, "main-empty.png");
            ((TextBox)view.FindName("RawText")).Text = "忠实的翻译始于准确的原文。\n\n本研究表明，保留上下文、技术术语和数字信息，对于准确理解论文中的结论至关重要。模型不应补充原文未包含的内容。\n\n这是用于检查布局的演示文本，不是真实模型返回结果。";
            ((StackPanel)view.FindName("EmptyState")).Visibility = Visibility.Collapsed;
            ((TextBlock)view.FindName("StatusText")).Text = "界面演示  ·  流式译文区域";
            ((TextBlock)view.FindName("SourceText")).Text = "chrome  ·  自动复制  ·  取词耗时将在这里显示";
            Save(view, 750, 635, "main-translation.png");
            Save(view, 580, 480, "main-small.png");
            CheckFits(view, "SettingsButton", "ModeButton", "PasteButton", "HideButton", "FixedPosition", "RawText");
            string originalText = ((TextBox)view.FindName("RawText")).Text;
            WindowLayout.Apply(view, true);
            Save(view, 400, 340, "main-compact.png");
            Save(view, 320, 300, "main-compact-small.png");
            CheckFits(view, "SettingsButton", "ModeButton", "PasteButton", "HideButton", "FixedPosition", "RawText");
            if (((TextBox)view.FindName("RawText")).ActualHeight < 50) throw new Exception("Compact text viewport is too small");
            WindowLayout.Apply(view, false);
            if (((TextBox)view.FindName("RawText")).Text != originalText) throw new Exception("Switching mode discarded translation");
            if (((FrameworkElement)view.FindName("Hero")).Visibility != Visibility.Visible) throw new Exception("Normal mode did not restore layout");
            var settings = new TranslatorSettings { ApiKey = "fake-ui-test-key" };
            var window = new SettingsWindow(settings, new SettingsStore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-unused.json")));
            Save((FrameworkElement)window.Content, 600, 740, "settings.png");
            // Walk this in-memory test view, without opening or operating any desktop window.
            var panel = (StackPanel)((ScrollViewer)window.Content).Content;
            ComboBox preset = null; PasswordBox key = null;
            foreach (UIElement item in panel.Children)
            {
                if (preset == null && item is ComboBox) preset = (ComboBox)item;
                if (item is PasswordBox) key = (PasswordBox)item;
            }
            preset.SelectedIndex = 1;
            if (key.Password != "fake-ui-test-key") throw new Exception("Changing model within same provider lost key");
            preset.SelectedIndex = 2;
            if (key.Password.Length != 0) throw new Exception("Switching provider retained previous key");
        }
        private static void CheckFits(FrameworkElement view, params string[] names)
        {
            foreach (string name in names)
            {
                var element = (FrameworkElement)view.FindName(name);
                var origin = element.TranslatePoint(new Point(), view);
                if (origin.X < 0 || origin.Y < 0 || origin.X + element.ActualWidth > view.ActualWidth + 1 || origin.Y + element.ActualHeight > view.ActualHeight + 1)
                    throw new Exception("Control is clipped: " + name);
            }
        }
        private static void Save(FrameworkElement view, int width, int height, string name)
        {
            view.Width = width; view.Height = height;
            view.Measure(new Size(width, height)); view.Arrange(new Rect(0, 0, width, height)); view.UpdateLayout();
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); image.Render(view);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (var output = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name))) encoder.Save(output);
        }
    }
}
