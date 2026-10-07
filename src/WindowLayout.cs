using System.Windows;
using System.Windows.Controls;

namespace Shici
{
    internal static class WindowLayout
    {
        internal static void Apply(FrameworkElement view, bool compact)
        {
            ((Grid)view.FindName("LayoutRoot")).Margin = compact ? new Thickness(12, 10, 12, 12) : new Thickness(26, 22, 26, 22);
            ((Grid)view.FindName("Header")).Margin = new Thickness(0, 0, 0, compact ? 10 : 20);
            foreach (string name in new[] { "Hero", "BrandSubtitle", "BrandIcon", "SourceText", "DetailNotes", "EmptyIcon", "EmptyFootnote" })
                ((FrameworkElement)view.FindName(name)).Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            ((TextBlock)view.FindName("BrandName")).FontSize = compact ? 17 : 25;
            ((Button)view.FindName("ModeButton")).Content = compact ? "正常版" : "简洁版";
            ((Button)view.FindName("SettingsButton")).Content = compact ? "设置" : "模型设置";
            ((Border)view.FindName("StatusPanel")).Padding = compact ? new Thickness(9, 6, 9, 6) : new Thickness(13, 10, 13, 10);
            ((Border)view.FindName("StatusPanel")).Margin = new Thickness(0, 0, 0, compact ? 8 : 14);
            ((Grid)view.FindName("TextPanel")).Margin = compact ? new Thickness(10, 8, 10, 8) : new Thickness(18, 14, 18, 12);
            ((TextBlock)view.FindName("EmptyTitle")).FontSize = compact ? 14 : 17;
            ((StackPanel)view.FindName("EmptyState")).Margin = new Thickness(compact ? 8 : 20);
            ((TextBox)view.FindName("RawText")).FontSize = compact ? 14 : 15;
            ((RadioButton)view.FindName("OriginalTab")).Content = compact ? "原文" : "原始文本";
            ((RadioButton)view.FindName("PreviewTab")).Content = compact ? "整理" : "整理预览";
            foreach (string name in new[] { "TranslationTab", "OriginalTab", "PreviewTab" })
                ((RadioButton)view.FindName(name)).Padding = compact ? new Thickness(9, 5, 9, 5) : new Thickness(15, 8, 15, 8);
            foreach (string name in new[] { "ModeButton", "SettingsButton", "PasteButton", "TranslateButton", "CopyButton", "HideButton" })
                ((Button)view.FindName(name)).Padding = compact ? new Thickness(10, 7, 10, 7) : new Thickness(14, 9, 14, 9);
            ((Button)view.FindName("HideButton")).Content = compact ? "收起" : "收起到托盘";
        }
    }
}
