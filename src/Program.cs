using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;

[assembly: AssemblyTitle("拾词 · AI 划词翻译")]
[assembly: AssemblyDescription("Chrome / PDF / Zotero selected-text probe")]
[assembly: AssemblyVersion("0.3.0.0")]

namespace Shici
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 4 && args[0] == "--uia") return CaptureEngine.Worker(args);
            bool created;
            using (var mutex = new Mutex(true, "Local\\Shici.SelectionProbe", out created))
            {
                if (!created)
                {
                    IntPtr existing = Native.FindWindow(null, MainWindow.WindowTitle);
                    if (existing == IntPtr.Zero)
                    {
                        IntPtr old = Native.FindWindow(null, "拾词 · 取词验证版");
                        if (old != IntPtr.Zero) Native.PostMessage(old, Native.WM_SHOWAPP, IntPtr.Zero, IntPtr.Zero);
                        MessageBox.Show("旧版拾词仍在后台运行。请右键旧版托盘图标，选择「退出拾词」，然后重新打开新版。", "拾词 · 更新提示");
                    }
                    else Native.PostMessage(existing, Native.WM_SHOWAPP, IntPtr.Zero, IntPtr.Zero);
                    return 0;
                }
                try
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    var window = new MainWindow();
                    app.MainWindow = window;
                    window.Show();
                    if (Array.IndexOf(args, "--background") >= 0) window.Hide();
                    return app.Run();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("拾词启动失败：\n" + ex.Message, "拾词", MessageBoxButton.OK, MessageBoxImage.Error);
                    return 1;
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
    }
}
