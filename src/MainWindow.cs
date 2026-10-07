using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Shici
{
    internal sealed class MainWindow : Window
    {
        internal const string WindowTitle = "拾词 · AI 划词翻译";
        private readonly FrameworkElement view;
        private readonly Forms.NotifyIcon tray;
        private readonly Icon trayIcon;
        private HwndSource source;
        private IntPtr handle;
        private bool registered, busy, quitting, translating, settingsOpen;
        private string original = "";
        private string translated = "";
        private int generation;
        private readonly TranslationClient translator = new TranslationClient();
        private readonly SettingsStore settingsStore = new SettingsStore();
        private TranslatorSettings settings;
        private CancellationTokenSource translationCancellation;
        private string settingsNotice;
        private readonly WindowPreferencesStore windowStore = new WindowPreferencesStore();
        private readonly WindowPreferences windowPreferences;
        private readonly DispatcherTimer windowSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        private bool changingWindow, presentationReady;
        private T Find<T>(string name) where T : FrameworkElement { return (T)view.FindName(name); }

        internal MainWindow()
        {
            Title = WindowTitle;
            windowPreferences = windowStore.Load();
            ResizeMode = ResizeMode.CanResizeWithGrip;
            WindowStyle = WindowStyle.SingleBorderWindow;
            FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
            WindowStartupLocation = windowPreferences.Current.HasPosition ? WindowStartupLocation.Manual : WindowStartupLocation.CenterScreen;
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 247, 246));
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Shici.Main.xaml"))
                view = (FrameworkElement)XamlReader.Load(stream);
            Content = view;
            settings = settingsStore.Load(out settingsNotice);
            ApplyWindowLayout();
            Topmost = windowPreferences.AlwaysOnTop;
            Find<CheckBox>("PinWindow").IsChecked = windowPreferences.AlwaysOnTop;
            Find<CheckBox>("FixedPosition").IsChecked = windowPreferences.FixedPosition;
            trayIcon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            Icon = Imaging.CreateBitmapSourceFromHIcon(trayIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "拾词 · Alt + E 翻译", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开取词窗口", null, (s, e) => Dispatcher.Invoke(new Action(ShowTool)));
            menu.Items.Add("模型设置", null, (s, e) => Dispatcher.Invoke(new Action(OpenSettings)));
            menu.Items.Add("退出拾词", null, (s, e) => Dispatcher.Invoke(new Action(RequestExit)));
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (s, e) => Dispatcher.Invoke(new Action(ShowTool));
            Find<Button>("HideButton").Click += (s, e) => HideTool();
            Find<Button>("ModeButton").Click += (s, e) => ToggleWindowMode();
            Find<Button>("CopyButton").Click += (s, e) => CopyDisplayed();
            Find<Button>("PasteButton").Click += (s, e) => PasteOriginal();
            Find<Button>("RetryHotkeyButton").Click += (s, e) => RegisterShortcut();
            Find<Button>("SettingsButton").Click += (s, e) => OpenSettings();
            Find<Button>("TranslateButton").Click += (s, e) => { if (translating) CancelTranslation(true); else StartTranslation(); };
            Find<RadioButton>("TranslationTab").Checked += (s, e) => RefreshText();
            Find<RadioButton>("OriginalTab").Checked += (s, e) => RefreshText();
            Find<RadioButton>("PreviewTab").Checked += (s, e) => RefreshText();
            Find<CheckBox>("PinWindow").Checked += (s, e) => { Topmost = true; windowPreferences.AlwaysOnTop = true; SaveWindowPreferences(); };
            Find<CheckBox>("PinWindow").Unchecked += (s, e) => { Topmost = false; windowPreferences.AlwaysOnTop = false; SaveWindowPreferences(); };
            Find<CheckBox>("FixedPosition").Checked += (s, e) => { windowPreferences.FixedPosition = true; SaveWindowPreferences(); };
            Find<CheckBox>("FixedPosition").Unchecked += (s, e) => { windowPreferences.FixedPosition = false; SaveWindowPreferences(); };
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) { CancelTranslation(true); HideTool(); e.Handled = true; } };
            windowSaveTimer.Tick += (s, e) => SaveWindowPreferences();
            LocationChanged += (s, e) => ScheduleWindowSave();
            SizeChanged += (s, e) => ScheduleWindowSave();
            StateChanged += (s, e) => ScheduleWindowSave();
            ContentRendered += (s, e) =>
            {
                if (presentationReady) return;
                changingWindow = true;
                RestoreWindowPosition();
                if (windowPreferences.Current.Maximized) WindowState = WindowState.Maximized;
                changingWindow = false;
                presentationReady = true;
            };
            SourceInitialized += (s, e) =>
            {
                handle = new WindowInteropHelper(this).Handle;
                source = HwndSource.FromHwnd(handle);
                source.AddHook(WindowMessage);
                RegisterShortcut();
                if (settingsNotice != null) SetStatus(settingsNotice, true);
                else if (registered && !settings.Ready) SetStatus("先打开右上角「模型设置」填写 API Key；也可直接按 Alt + E 验证取词。", false);
            };
            Closing += OnClosing;
            Closed += (s, e) =>
            {
                windowSaveTimer.Stop();
                if (registered) Native.UnregisterHotKey(handle, Native.HotkeyId);
                if (source != null) source.RemoveHook(WindowMessage);
                tray.Visible = false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); trayIcon.Dispose();
                CancelTranslation(false); translator.Dispose();
                Application.Current.Shutdown();
            };
        }

        private void RegisterShortcut()
        {
            if (registered) Native.UnregisterHotKey(handle, Native.HotkeyId);
            registered = Native.RegisterHotKey(handle, Native.HotkeyId, Native.HotkeyModifiers, Native.HotkeyKey);
            Find<Button>("RetryHotkeyButton").Visibility = registered ? Visibility.Collapsed : Visibility.Visible;
            SetStatus(registered ? "准备好了  ·  在其他窗口选中文字，按 Alt + E" : "快捷键 Alt + E 被其他程序占用。释放后点击下方按钮重试。", !registered);
        }

        private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (message == Native.WM_HOTKEY && wParam.ToInt32() == Native.HotkeyId)
            {
                handled = true;
                if (!busy && !quitting && !settingsOpen) BeginCapture();
            }
            else if (message == Native.WM_SHOWAPP) { handled = true; ShowTool(); }
            return IntPtr.Zero;
        }

        private async void BeginCapture()
        {
            IntPtr target = Native.GetForegroundWindow();
            if (target == IntPtr.Zero || Native.ProcessId(target) == (uint)Process.GetCurrentProcess().Id)
            {
                SetStatus("请先切换到 Chrome 或 Zotero，选中文字后再按快捷键。", true);
                return;
            }
            Native.Point cursor; Native.GetCursorPos(out cursor);
            Native.MaskAltMenu(target);
            CancelTranslation(false);
            busy = true;
            Find<Button>("SettingsButton").IsEnabled = false;
            Find<Button>("CopyButton").IsEnabled = false;
            // Clear prior output immediately: a failed capture must not appear to have reused it.
            original = ""; translated = ""; RefreshText();
            Find<TextBlock>("SourceText").Text = "正在读取当前窗口的选区…";
            Find<TextBlock>("ClipboardText").Text = "";
            SetStatus("正在取词，请稍候…", false);
            tray.Text = "拾词 · 正在取词";
            try
            {
                var result = await CaptureEngine.Capture(target, cursor, handle, settings.CaptureMode);
                original = result.Success ? result.Text : "";
                Find<RadioButton>("OriginalTab").IsChecked = true;
                RefreshText();
                SetStatus(result.Success ? "已取得原文  ·  请与选中的内容核对" : result.Error ?? "未取得选中文字，请重新划选后再试。", !result.Success);
                Find<TextBlock>("SourceText").Text = result.Source + "  ·  " + result.Method + "  ·  取词 " + result.Milliseconds + " ms";
                Find<TextBlock>("ClipboardText").Text = result.ClipboardNote ?? "剪贴板未被本工具修改";
                if (!quitting)
                {
                    if (Native.GetForegroundWindow() == target) ShowNear(cursor);
                    else tray.ShowBalloonTip(2500, "拾词", result.Success ? "取词完成，双击托盘图标查看原文。" : "取词未完成，双击托盘图标查看原因。", Forms.ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                original = ""; RefreshText();
                SetStatus("取词失败：" + ex.Message, true);
                if (!quitting && Native.GetForegroundWindow() == target) ShowNear(cursor);
            }
            finally
            {
                busy = false; Find<Button>("SettingsButton").IsEnabled = true;
                RefreshText();
                tray.Text = "拾词 · Alt + E 翻译";
                if (quitting) Close();
            }
            if (!quitting && original.Length > 0 && settings.AutoTranslate && settings.Ready) StartTranslation();
            else if (!quitting && original.Length > 0 && !settings.Ready) SetStatus("已取得原文。打开「模型设置」填写 API Key 后，即可开始翻译。", false);
        }

        private void RefreshText()
        {
            bool preview = Find<RadioButton>("PreviewTab").IsChecked == true;
            bool translation = Find<RadioButton>("TranslationTab").IsChecked == true;
            string shown = translation ? translated : preview ? TextTools.Normalize(original) : original;
            if (Find<TextBox>("RawText").Text != shown) Find<TextBox>("RawText").Text = shown;
            Find<StackPanel>("EmptyState").Visibility = shown.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            Find<TextBlock>("EmptyTitle").Text = translating ? "正在生成译文…" : original.Length > 0 ? "原文已经准备好了" : "先去划选一段文字";
            Find<TextBlock>("EmptyDescription").Text = translating ? "模型开始输出后，译文会逐步显示。" : settings.Ready ? "按 Alt + E 取词，或点击下方「翻译」。" : "首次使用，请在右上角设置模型与 API Key。";
            Find<TextBlock>("CountText").Text = original.Length == 0 ? "等待取词" : original.Length.ToString("N0") + " 字符";
            Find<TextBlock>("PreviewNote").Text = translation ? settings.Model + "  ·  " + (settings.TargetLanguage == "Chinese" ? "简体中文" : settings.TargetLanguage == "English" ? "英语" : "日语") + "  ·  不保存翻译历史" : preview
                ? "仅保守合并换行和软连字符；普通连字符、列表及公式保留。原始文本不会改变。"
                : "原始文本未经整理；可切换预览，对比 PDF 的换行。";
            Find<Button>("CopyButton").Content = translation ? "复制译文" : preview ? "复制预览" : "复制原文";
            Find<Button>("CopyButton").IsEnabled = shown.Length > 0 && !busy;
            Find<Button>("PasteButton").IsEnabled = !busy;
            Find<Button>("TranslateButton").Content = translating ? "停止" : "翻译";
            Find<Button>("TranslateButton").IsEnabled = original.Length > 0 && !busy;
        }

        private void PasteOriginal()
        {
            if (busy) return;
            try
            {
                // Deliberate button click only; the global hotkey never reads old content.
                string text = Clipboard.GetText(TextDataFormat.UnicodeText);
                if (string.IsNullOrWhiteSpace(text)) { SetStatus("剪贴板没有文字。请先在原页面选中文字并按 Ctrl+C。", true); return; }
                if (text.Length > TextTools.MaxLength) { SetStatus("原文超过 10 万字符，请缩小选择范围。", true); return; }
                CancelTranslation(false);
                original = text; translated = "";
                Find<RadioButton>("OriginalTab").IsChecked = true;
                RefreshText();
                Find<TextBlock>("SourceText").Text = "手动粘贴  ·  请确认显示的原文就是需要翻译的选区";
                Find<TextBlock>("ClipboardText").Text = "剪贴板未修改";
                SetStatus("已粘贴原文。核对后点击「翻译」。", false);
            }
            catch { SetStatus("暂时无法读取剪贴板，请稍后重试。", true); }
        }

        private void OpenSettings()
        {
            if (busy || settingsOpen) return;
            CancelTranslation(true); ShowTool(); settingsOpen = true;
            try
            {
                var dialog = new SettingsWindow(settings, settingsStore) { Owner = this };
                if (dialog.ShowDialog() == true)
                {
                    settings = dialog.Saved; translator.ClearCache();
                    translated = ""; RefreshText();
                    SetStatus(settings.Ready ? "设置已保存。重新划词，或点击「翻译」使用当前原文。" : "设置已保存。填入 API Key 后可使用翻译；取词功能已可用。", false);
                }
            }
            finally { settingsOpen = false; }
        }

        private void CancelTranslation(bool explain)
        {
            generation++;
            if (translationCancellation != null) { translationCancellation.Cancel(); translationCancellation = null; }
            bool wasTranslating = translating; translating = false;
            if (explain && wasTranslating) SetStatus("已停止翻译，已显示的内容可能不完整。", false);
            RefreshText();
        }

        private async void StartTranslation()
        {
            if (busy || string.IsNullOrWhiteSpace(original)) return;
            if (!settings.Ready) { OpenSettings(); return; }
            CancelTranslation(false);
            int currentGeneration = generation;
            var cancellation = new CancellationTokenSource();
            translationCancellation = cancellation;
            var requestSettings = settings.Clone();
            string text = requestSettings.PrepareText ? TextTools.Normalize(original) : original;
            translating = true; translated = "";
            Find<RadioButton>("TranslationTab").IsChecked = true; RefreshText();
            SetStatus("正在翻译  ·  " + requestSettings.Model, false);
            try
            {
                var result = await translator.Translate(requestSettings, text, value => Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (generation != currentGeneration || quitting || !translating) return;
                    translated = value; RefreshText();
                })), cancellation.Token);
                if (generation != currentGeneration || quitting) return;
                translated = result.Text; RefreshText();
                SetStatus(result.FromCache ? "翻译完成  ·  命中本次会话缓存，无需再次请求模型" : "翻译完成  ·  首段 " + (result.FirstTextMilliseconds / 1000.0).ToString("0.00") + " s  ·  总计 " + (result.TotalMilliseconds / 1000.0).ToString("0.00") + " s", false);
            }
            catch (OperationCanceledException) { if (generation == currentGeneration && !quitting) SetStatus("已停止翻译，当前内容可能不完整。", false); }
            catch (Exception ex) { if (generation == currentGeneration && !quitting) SetStatus("翻译失败：" + ex.Message, true); }
            finally
            {
                cancellation.Dispose();
                if (generation == currentGeneration)
                {
                    translationCancellation = null; translating = false;
                    if (!quitting) RefreshText();
                }
            }
        }

        private void SetStatus(string message, bool error)
        {
            Find<TextBlock>("StatusText").Text = message;
            Find<TextBlock>("StatusText").Foreground = new SolidColorBrush(error ? System.Windows.Media.Color.FromRgb(150, 77, 44) : System.Windows.Media.Color.FromRgb(71, 100, 82));
            Find<Border>("StatusPanel").Background = new SolidColorBrush(error ? System.Windows.Media.Color.FromRgb(251, 237, 225) : System.Windows.Media.Color.FromRgb(234, 240, 236));
        }
        private void CopyDisplayed()
        {
            try
            {
                System.Windows.Clipboard.SetText(Find<TextBox>("RawText").Text);
                Find<TextBlock>("ClipboardText").Text = "已按你的操作复制当前文本到剪贴板";
            }
            catch { SetStatus("剪贴板正被其他软件占用，请稍后再点击复制。", true); }
        }
        private void ShowTool()
        {
            Show();
            RestoreIfMinimized();
            KeepWindowReachable();
            Activate();
        }
        private void ShowNear(Native.Point point)
        {
            changingWindow = true;
            ShowActivated = false;
            Show();
            RestoreIfMinimized();
            if (windowPreferences.FollowCursor(WindowState == WindowState.Maximized))
                PositionWindow(point, point.X + 18, point.Y + 20, true);
            else KeepWindowReachable();
            ShowActivated = true; Activate();
            changingWindow = false;
            SaveWindowPreferences();
        }
        private void RestoreIfMinimized()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = windowPreferences.Current.Maximized ? WindowState.Maximized : WindowState.Normal;
        }
        private void ApplyWindowLayout()
        {
            MinWidth = windowPreferences.Compact ? 340 : 600;
            MinHeight = windowPreferences.Compact ? 340 : 520;
            Width = windowPreferences.Current.Width;
            Height = windowPreferences.Current.Height;
            WindowLayout.Apply(view, windowPreferences.Compact);
        }
        private void ToggleWindowMode()
        {
            SaveWindowPreferences();
            var previous = windowPreferences.Current;
            changingWindow = true;
            windowPreferences.Compact = !windowPreferences.Compact;
            var next = windowPreferences.Current;
            if (!next.HasPosition && previous.HasPosition)
            {
                next.Left = previous.Left; next.Top = previous.Top; next.HasPosition = true;
            }
            WindowState = WindowState.Normal;
            ApplyWindowLayout();
            RestoreWindowPosition();
            if (next.Maximized) WindowState = WindowState.Maximized;
            changingWindow = false;
            RefreshText();
            SaveWindowPreferences();
        }
        private void RestoreWindowPosition()
        {
            var geometry = windowPreferences.Current;
            if (geometry.HasPosition)
                PositionWindow(new Native.Point { X = geometry.Left, Y = geometry.Top }, geometry.Left, geometry.Top, true);
        }
        private void KeepWindowReachable()
        {
            if (WindowState != WindowState.Normal || handle == IntPtr.Zero) return;
            Native.Rect rect;
            if (Native.GetWindowRect(handle, out rect))
                PositionWindow(new Native.Point { X = rect.Left, Y = rect.Top }, rect.Left, rect.Top, false);
        }
        private void PositionWindow(Native.Point point, int proposedLeft, int proposedTop, bool moveMonitor)
        {
            if (handle == IntPtr.Zero) return;
            var info = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MonitorInfo)) };
            if (Native.GetMonitorInfo(Native.MonitorFromPoint(point, 2), ref info))
            {
                // Move first only when changing monitors; fixed windows avoid this jump.
                if (moveMonitor && Native.MonitorFromWindow(handle, 2) != Native.MonitorFromPoint(point, 2))
                    Native.SetWindowPos(handle, IntPtr.Zero, info.Work.Left, info.Work.Top, 0, 0, 0x15);
                UpdateLayout();
                Native.Rect rect;
                if (!Native.GetWindowRect(handle, out rect)) return;
                int left = WindowPreferences.ClampPosition(proposedLeft, rect.Right - rect.Left, info.Work.Left, info.Work.Right);
                int top = WindowPreferences.ClampPosition(proposedTop, rect.Bottom - rect.Top, info.Work.Top, info.Work.Bottom);
                if (left != rect.Left || top != rect.Top)
                    Native.SetWindowPos(handle, IntPtr.Zero, left, top, 0, 0, 0x15); // Preserve the user's size.
            }
        }
        private void ScheduleWindowSave()
        {
            if (!presentationReady || changingWindow || !IsVisible) return;
            CaptureWindowGeometry();
            windowSaveTimer.Stop(); windowSaveTimer.Start();
        }
        private void SaveWindowPreferences()
        {
            windowSaveTimer.Stop();
            if (!presentationReady || changingWindow) return;
            CaptureWindowGeometry();
            try { windowStore.Save(windowPreferences); }
            catch { Find<CheckBox>("FixedPosition").ToolTip = "当前设置已在本次运行生效，但未能保存到磁盘；请检查用户目录是否可写。"; }
        }
        private void CaptureWindowGeometry()
        {
            var geometry = windowPreferences.Current;
            if (WindowState == WindowState.Normal)
            {
                Native.Rect rect;
                if (Native.GetWindowRect(handle, out rect))
                {
                    geometry.Left = rect.Left; geometry.Top = rect.Top; geometry.HasPosition = true;
                    geometry.Width = Width; geometry.Height = Height;
                }
                geometry.Maximized = false;
            }
            else if (WindowState == WindowState.Maximized) geometry.Maximized = true;
        }
        private void HideTool()
        {
            SaveWindowPreferences();
            Hide();
        }
        private void RequestExit()
        {
            CancelTranslation(false);
            quitting = true;
            if (busy) SetStatus("正在结束本次取词，等待剪贴板处理完成后退出…", false);
            else Close();
        }
        private void OnClosing(object sender, CancelEventArgs e)
        {
            SaveWindowPreferences();
            if (!quitting) { e.Cancel = true; CancelTranslation(true); HideTool(); }
        }
    }
}
