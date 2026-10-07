using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace Shici
{
    internal static class CaptureEngine
    {
        internal static async Task<CaptureResult> Capture(IntPtr target, Native.Point cursor, IntPtr owner, string captureMode)
        {
            var timer = Stopwatch.StartNew();
            string source = "未知程序";
            try { using (var app = Process.GetProcessById((int)Native.ProcessId(target))) source = app.ProcessName; }
            catch { }
            string preparationError = await ReleaseGate.Wait(target);
            CaptureResult result = preparationError != null
                ? new CaptureResult { Error = preparationError, Method = "等待按键释放" }
                : await CapturePolicy.Read(CapturePolicy.UseCopyFirst(source, captureMode), captureMode != "copy",
                    () => Task.Run(() => ClipboardCapture.Read(target, owner)),
                    () => ReadAutomation(target, cursor), () => Native.GetForegroundWindow() == target);
            if (result.Success && Native.GetForegroundWindow() != target)
            {
                result.Text = null;
                result.Error = "取词期间切换了窗口，已取消。请重新触发。";
            }
            result.Source = source;
            result.Milliseconds = timer.ElapsedMilliseconds;
            return result;
        }

        private static async Task<CaptureResult> ReadAutomation(IntPtr target, Native.Point cursor)
        {
            var result = new CaptureResult { Method = "系统选区", ClipboardNote = "未使用剪贴板", CanFallback = true };
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = System.Reflection.Assembly.GetExecutingAssembly().Location,
                    Arguments = "--uia " + target.ToInt64() + " " + cursor.X + " " + cursor.Y,
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                try
                {
                    process.Start();
                    Task<string> output = process.StandardOutput.ReadToEndAsync();
                    Task<string> error = process.StandardError.ReadToEndAsync();
                    Task exit = Task.Run(() => process.WaitForExit());
                    if (await Task.WhenAny(exit, Task.Delay(900)) != exit)
                    {
                        try { process.Kill(); } catch { }
                        await Task.WhenAny(exit, Task.Delay(300));
                        return result;
                    }
                    string reply = (await output).Trim();
                    await error;
                    if (reply.StartsWith("OK:")) result.Text = Encoding.UTF8.GetString(Convert.FromBase64String(reply.Substring(3)));
                    else if (reply == "PROTECTED") { result.Error = "当前为密码输入区域，不读取内容。"; result.CanFallback = false; }
                    else if (reply == "LONG") { result.Error = "选区超过 10 万字符，请缩小选择范围。"; result.CanFallback = false; }
                }
                catch { /* Provider or helper failure falls back to a fresh copy. */ }
            }
            return result;
        }

        // UI Automation providers can block indefinitely. This runs only in a disposable child process.
        internal static int Worker(string[] args)
        {
            string response = "EMPTY";
            var thread = new Thread(() =>
            {
                try
                {
                    IntPtr target = new IntPtr(long.Parse(args[1]));
                    int x = int.Parse(args[2]), y = int.Parse(args[3]);
                    if (Native.GetForegroundWindow() != target) return;
                    // Read the focused selection before invoking FromPoint. A slow
                    // point provider must not discard an already available selection.
                    try
                    {
                        response = ReadSelection(AutomationElement.FocusedElement, target);
                        if (response != "EMPTY") return;
                    }
                    catch { }
                    if (Native.GetForegroundWindow() == target)
                        response = ReadSelection(AutomationElement.FromPoint(new System.Windows.Point(x, y)), target);
                }
                catch { }
            });
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start(); thread.Join();
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.Write(response);
            return 0;
        }
        private static string ReadSelection(AutomationElement element, IntPtr target)
        {
            if (element == null || !BelongsTo(element, target)) return "EMPTY";
            for (int depth = 0; element != null && depth < 14; depth++)
            {
                if (element.Current.IsPassword) return "PROTECTED";
                object pattern;
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                {
                    var ranges = ((TextPattern)pattern).GetSelection();
                    var text = new StringBuilder();
                    if (ranges != null) foreach (var range in ranges)
                    {
                        string fragment = range.GetText(TextTools.MaxLength + 1);
                        if (fragment.Length == 0) continue;
                        if (text.Length > 0) text.AppendLine();
                        text.Append(fragment);
                        if (text.Length > TextTools.MaxLength) return "LONG";
                    }
                    if (!string.IsNullOrWhiteSpace(text.ToString()) && Native.GetForegroundWindow() == target)
                        return "OK:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text.ToString()));
                }
                if (element.Current.NativeWindowHandle == target.ToInt64()) break;
                element = TreeWalker.RawViewWalker.GetParent(element);
            }
            return "EMPTY";
        }
        private static bool BelongsTo(AutomationElement element, IntPtr target)
        {
            for (int i = 0; element != null && i < 24; i++)
            {
                IntPtr handle = new IntPtr(element.Current.NativeWindowHandle);
                if (handle == target || (handle != IntPtr.Zero && Native.GetAncestor(handle, 2) == target)) return true;
                element = TreeWalker.RawViewWalker.GetParent(element);
            }
            return false;
        }
    }
}
