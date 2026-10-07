using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Shici
{
    internal sealed class SettingsWindow : Window
    {
        private readonly ComboBox preset = new ComboBox(), protocol = new ComboBox(), language = new ComboBox(), capture = new ComboBox();
        private readonly TextBox endpoint = new TextBox(), model = new TextBox();
        private readonly PasswordBox key = new PasswordBox();
        private readonly CheckBox auto = new CheckBox { Content = "取词后自动翻译", Margin = new Thickness(0, 8, 0, 8) };
        private readonly CheckBox prepare = new CheckBox { Content = "翻译前保守整理 PDF 换行（原文仍保留）", Margin = new Thickness(0, 0, 0, 14) };
        private readonly TextBlock hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0, 8, 0, 8), FontSize = 12 };
        private readonly TextBlock error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Firebrick, Margin = new Thickness(0, 8, 0, 8) };
        private string keyAuthority;
        internal TranslatorSettings Saved;

        internal SettingsWindow(TranslatorSettings current, SettingsStore store)
        {
            Title = "拾词 · 模型设置"; Width = 620; Height = 775; MinWidth = 540; MinHeight = 580;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FontFamily = new FontFamily("Microsoft YaHei UI"); FontSize = 13;
            Background = new SolidColorBrush(Color.FromRgb(245, 247, 246));
            var panel = new StackPanel { Margin = new Thickness(27, 22, 27, 22) };
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Background, Foreground = Brushes.Black, FontFamily = FontFamily, FontSize = FontSize };
            panel.Children.Add(new TextBlock { Text = "选择你的翻译模型", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 15) });
            foreach (var item in ModelPreset.All) preset.Items.Add(item);
            AddField(panel, "模型预设", preset);
            panel.Children.Add(hint);
            AddField(panel, "接口地址（Base URL，可修改地域或业务空间域名）", endpoint);
            AddField(panel, "模型名称", model);
            protocol.Items.Add("Qwen-MT 专用翻译"); protocol.Items.Add("DeepSeek · 关闭思考"); protocol.Items.Add("OpenAI · 关闭思考"); protocol.Items.Add("通用 Chat Completions");
            AddField(panel, "请求协议", protocol);
            AddField(panel, "API Key（仅在本机当前账户下加密保存）", key);
            language.Items.Add("简体中文"); language.Items.Add("英语"); language.Items.Add("日语");
            AddField(panel, "翻译成", language);
            capture.Items.Add("自动 · Chrome / Zotero 优先直接复制（推荐）"); capture.Items.Add("始终直接复制"); capture.Items.Add("优先系统选区 · 失败后复制");
            AddField(panel, "取词方式", capture);
            panel.Children.Add(auto); panel.Children.Add(prepare);
            panel.Children.Add(new TextBlock { Text = "翻译会将本次选中的文字发送到上方接口，按服务商规则计费。不会发送整份 PDF 或网页，也不保存原文/译文历史。\n价格参考日期：2026-10-05，实际以服务商控制台为准。", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brushes.DimGray });
            panel.Children.Add(error);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var cancel = new Button { Content = "取消", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 0, 12, 0), IsCancel = true };
            var save = new Button { Content = "保存设置", Padding = new Thickness(20, 8, 20, 8), Background = new SolidColorBrush(Color.FromRgb(24, 92, 69)), Foreground = Brushes.White, IsDefault = true };
            buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);

            endpoint.Text = current.BaseUrl; model.Text = current.Model; key.Password = current.ApiKey ?? "";
            keyAuthority = Authority(endpoint.Text);
            protocol.SelectedIndex = ProtocolIndex(current.Protocol);
            language.SelectedIndex = current.TargetLanguage == "English" ? 1 : current.TargetLanguage == "Japanese" ? 2 : 0;
            capture.SelectedIndex = current.CaptureMode == "copy" ? 1 : current.CaptureMode == "system" ? 2 : 0;
            auto.IsChecked = current.AutoTranslate; prepare.IsChecked = current.PrepareText;
            int selected = ModelPreset.All.Length - 1;
            for (int i = 0; i < ModelPreset.All.Length; i++) if (ModelPreset.All[i].Model == current.Model && ModelPreset.All[i].Protocol == current.Protocol) { selected = i; break; }
            preset.SelectedIndex = selected; hint.Text = ModelPreset.All[selected].Hint;
            preset.SelectionChanged += (s, e) =>
            {
                var value = (ModelPreset)preset.SelectedItem;
                hint.Text = value.Hint; protocol.SelectedIndex = ProtocolIndex(value.Protocol);
                if (value.Model.Length > 0) { endpoint.Text = value.BaseUrl; model.Text = value.Model; }
            };
            endpoint.TextChanged += (s, e) =>
            {
                string authority = Authority(endpoint.Text);
                if (!string.Equals(authority, keyAuthority, StringComparison.OrdinalIgnoreCase))
                {
                    key.Password = ""; keyAuthority = authority;
                    error.Text = "接口服务地址已改变，请填写对应服务商的 API Key。";
                }
            };
            save.Click += (s, e) =>
            {
                try
                {
                    var value = new TranslatorSettings
                    {
                        BaseUrl = endpoint.Text.Trim().TrimEnd('/'), Model = model.Text.Trim(), ApiKey = key.Password.Trim(),
                        Protocol = new[] { "qwen-mt", "deepseek", "openai", "compatible" }[protocol.SelectedIndex],
                        TargetLanguage = new[] { "Chinese", "English", "Japanese" }[language.SelectedIndex],
                        CaptureMode = new[] { "auto", "copy", "system" }[capture.SelectedIndex],
                        AutoTranslate = auto.IsChecked == true, PrepareText = prepare.IsChecked == true
                    };
                    value.Validate(false); store.Save(value); Saved = value; DialogResult = true;
                }
                catch (Exception ex) { error.Text = ex.Message; }
            };
        }
        private static int ProtocolIndex(string value) { return value == "qwen-mt" ? 0 : value == "deepseek" ? 1 : value == "openai" ? 2 : 3; }
        private static string Authority(string value) { Uri uri; return Uri.TryCreate(value, UriKind.Absolute, out uri) ? uri.GetLeftPart(UriPartial.Authority) : ""; }
        private static void AddField(Panel panel, string label, Control input)
        {
            panel.Children.Add(new TextBlock { Text = label, Foreground = Brushes.DarkSlateGray, FontSize = 12, Margin = new Thickness(0, 5, 0, 5) });
            input.Padding = new Thickness(8, 6, 8, 6); input.Margin = new Thickness(0, 0, 0, 8); input.MinHeight = 32;
            panel.Children.Add(input);
        }
    }
}
