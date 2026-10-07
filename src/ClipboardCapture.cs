using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Shici
{
    internal sealed class ClipboardEntry : IDisposable
    {
        internal uint Format;
        internal byte[] Bytes;
        internal IntPtr Handle;
        public void Dispose()
        {
            if (Handle == IntPtr.Zero) return;
            if (Format == 2) Native.DeleteObject(Handle);
            else if (Format == 14) Native.DeleteEnhMetaFile(Handle);
            else Native.GlobalFree(Handle);
            Handle = IntPtr.Zero;
        }
        internal void Prepare()
        {
            if (Handle != IntPtr.Zero) return;
            Handle = Native.GlobalAlloc(0x0042, new UIntPtr((uint)Bytes.Length));
            if (Handle == IntPtr.Zero) throw new InvalidOperationException("无法为剪贴板恢复分配内存。");
            IntPtr pointer = Native.GlobalLock(Handle);
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("无法准备剪贴板恢复数据。");
            try { Marshal.Copy(Bytes, 0, pointer, Bytes.Length); }
            finally { Native.GlobalUnlock(Handle); }
        }
    }

    internal sealed class ClipboardSnapshot : IDisposable
    {
        internal readonly List<ClipboardEntry> Entries = new List<ClipboardEntry>();
        internal uint Sequence;
        public void Dispose() { foreach (var entry in Entries) entry.Dispose(); }

        // Called while holding OpenClipboard. Unknown non-memory formats abort before copying.
        internal static ClipboardSnapshot Read()
        {
            var snapshot = new ClipboardSnapshot();
            long total = 0;
            try
            {
                uint format = 0;
                while ((format = Native.EnumClipboardFormats(format)) != 0)
                {
                    if (snapshot.Entries.Count >= 128) throw new InvalidOperationException("剪贴板格式过多，已取消自动复制以保留原内容。");
                    if (format == 3 || format == 9 || (format >= 0x80 && format <= 0x8E) || (format >= 0x300 && format <= 0x3FF))
                        throw new InvalidOperationException("剪贴板含无法安全备份的特殊格式。请先手动复制普通文字后重试。");
                    IntPtr data = Native.GetClipboardData(format);
                    if (data == IntPtr.Zero) throw new InvalidOperationException("剪贴板中的某种格式暂时无法读取，已取消自动复制。");
                    var entry = new ClipboardEntry { Format = format };
                    snapshot.Entries.Add(entry);
                    if (format == 2)
                    {
                        entry.Handle = Native.CopyImage(data, 0, 0, 0, 0x2000);
                        if (entry.Handle == IntPtr.Zero) throw new InvalidOperationException("无法备份剪贴板图片，已取消自动复制。");
                    }
                    else if (format == 14)
                    {
                        entry.Handle = Native.CopyEnhMetaFile(data, null);
                        if (entry.Handle == IntPtr.Zero) throw new InvalidOperationException("无法备份剪贴板图元文件，已取消自动复制。");
                    }
                    else
                    {
                        ulong size = Native.GlobalSize(data).ToUInt64();
                        if (size == 0 || size > 16 * 1024 * 1024 || total + (long)size > 16 * 1024 * 1024)
                            throw new InvalidOperationException("剪贴板数据过大或包含特殊对象，已取消自动复制以保留原内容。");
                        total += (long)size;
                        IntPtr pointer = Native.GlobalLock(data);
                        if (pointer == IntPtr.Zero) throw new InvalidOperationException("无法安全备份剪贴板，已取消自动复制。");
                        try { entry.Bytes = new byte[(int)size]; Marshal.Copy(pointer, entry.Bytes, 0, (int)size); }
                        finally { Native.GlobalUnlock(data); }
                    }
                }
                snapshot.Sequence = Native.GetClipboardSequenceNumber();
                return snapshot;
            }
            catch { snapshot.Dispose(); throw; }
        }

        internal bool RestoreLocked(uint expectedSequence)
        {
            // Check under the lock: never overwrite something copied later by the user.
            if (!TextTools.CanRestore(expectedSequence, Native.GetClipboardSequenceNumber())) return false;
            foreach (var entry in Entries) entry.Prepare();
            if (!Native.EmptyClipboard()) throw new InvalidOperationException("无法恢复剪贴板。");
            bool failed = false;
            foreach (var entry in Entries)
            {
                if (Native.SetClipboardData(entry.Format, entry.Handle) == IntPtr.Zero) failed = true;
                else entry.Handle = IntPtr.Zero; // Windows owns it after successful SetClipboardData.
            }
            if (failed) throw new InvalidOperationException("剪贴板部分格式恢复失败，当前内容可能与取词前不同。");
            return true;
        }
    }

    internal static class ClipboardCapture
    {
        private static bool Open(IntPtr owner)
        {
            for (int i = 0; i < 10; i++)
            {
                if (Native.OpenClipboard(owner)) return true;
                Thread.Sleep(30);
            }
            return false;
        }

        // Run the complete operation on one worker thread: OpenClipboard and
        // CloseClipboard belong to the calling thread, even when a retry was needed.
        internal static CaptureResult Read(IntPtr target, IntPtr owner)
        {
            var result = new CaptureResult { Method = "自动复制" };
            var clock = Stopwatch.StartNew();
            uint process = Native.ProcessId(target);
            ClipboardSnapshot snapshot = null;
            uint copiedSequence = 0;
            bool ownsCopy = false;
            try
            {
                if (Native.CaptureKeysDown()) throw new InvalidOperationException("按键状态已改变，请松开 Alt 和 E 后再试。");
                if (Native.GetForegroundWindow() != target) throw new InvalidOperationException("取词期间切换了窗口，已取消。请在目标文本处重新触发。");
                if (!Open(owner)) throw new InvalidOperationException("剪贴板正被其他软件占用，请稍后重试。");
                try { snapshot = ClipboardSnapshot.Read(); }
                finally { Native.CloseClipboard(); }
                // Delayed clipboard rendering can take seconds; do not inject late keystrokes.
                if (clock.ElapsedMilliseconds > 3000) throw new InvalidOperationException("备份剪贴板耗时过长，已取消。请重新触发。");
                if (Native.GetForegroundWindow() != target || Native.CaptureKeysDown())
                    throw new InvalidOperationException("窗口或按键状态已改变，已取消自动复制。");
                if (Native.GetClipboardSequenceNumber() != snapshot.Sequence)
                    throw new InvalidOperationException("取词期间剪贴板已变化，已保留新内容。请重试。");
                if (!Native.SendCopy()) throw new InvalidOperationException("系统未接受复制快捷键。请确认目标程序没有以管理员身份运行。");
                result.ClipboardNote = "未确认新复制结果；未执行剪贴板恢复";
                var copyClock = Stopwatch.StartNew();
                while (copyClock.ElapsedMilliseconds < 1400)
                {
                    Thread.Sleep(15);
                    if (Native.GetForegroundWindow() != target)
                        throw new InvalidOperationException("取词期间切换了窗口，已取消。");
                    uint sequence = Native.GetClipboardSequenceNumber();
                    if (sequence == snapshot.Sequence) continue;
                    uint clipboardProcess = Native.ProcessId(Native.GetClipboardOwner());
                    if (!TextTools.IsFresh(snapshot.Sequence, sequence, process, clipboardProcess))
                    {
                        // Some applications briefly release ownership while preparing formats.
                        if (clipboardProcess == 0) continue;
                        throw new InvalidOperationException("其他程序更新了剪贴板，已取消取词并保留新内容。");
                    }
                    if (!Open(owner)) continue;
                    try
                    {
                        uint lockedSequence = Native.GetClipboardSequenceNumber();
                        uint lockedOwner = Native.ProcessId(Native.GetClipboardOwner());
                        if (!TextTools.IsFresh(snapshot.Sequence, lockedSequence, process, lockedOwner)) continue;
                        copiedSequence = lockedSequence;
                        ownsCopy = true;
                        IntPtr data = Native.GetClipboardData(13); // CF_UNICODETEXT
                        if (data == IntPtr.Zero) continue;
                        ulong size = Native.GlobalSize(data).ToUInt64();
                        if (size > (ulong)(TextTools.MaxLength + 1) * 2)
                            throw new InvalidOperationException("选区超过 10 万字符，请缩小选择范围。");
                        if (size < 2) continue;
                        IntPtr pointer = Native.GlobalLock(data);
                        if (pointer == IntPtr.Zero) continue;
                        try
                        {
                            byte[] bytes = new byte[(int)size];
                            Marshal.Copy(pointer, bytes, 0, bytes.Length);
                            string value = Encoding.Unicode.GetString(bytes);
                            int nul = value.IndexOf('\0');
                            if (nul >= 0) value = value.Substring(0, nul);
                            if (!string.IsNullOrWhiteSpace(value)) result.Text = value;
                        }
                        finally { Native.GlobalUnlock(data); }
                        // Delayed rendering may update the sequence while GetClipboardData runs.
                        copiedSequence = Native.GetClipboardSequenceNumber();
                    }
                    finally { Native.CloseClipboard(); }
                    if (result.Success) break;
                }
                if (!result.Success)
                {
                    result.CanFallback = true;
                    result.Error = "未收到选中文字的复制结果。可先在原页面按 Ctrl+C，再点下方「粘贴原文」。";
                }
            }
            catch (Exception ex) { result.Error = ex.Message; }
            {
                if (snapshot != null)
                {
                    if (ownsCopy)
                    {
                        try
                        {
                            if (Open(owner))
                            {
                                try
                                {
                                    result.ClipboardNote = snapshot.RestoreLocked(copiedSequence)
                                        ? "剪贴板已恢复" : "剪贴板已有新内容，未覆盖";
                                }
                                finally { Native.CloseClipboard(); }
                            }
                            else result.ClipboardNote = "剪贴板被占用，未能恢复；当前可能保留选中文字";
                        }
                        catch (Exception ex) { result.ClipboardNote = ex.Message; }
                    }
                    snapshot.Dispose();
                }
            }
            return result;
        }
    }
}
