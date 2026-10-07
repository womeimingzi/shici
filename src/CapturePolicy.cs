using System;
using System.Threading.Tasks;

namespace Shici
{
    internal static class CapturePolicy
    {
        internal static async Task<CaptureResult> Read(bool copyFirst, bool allowSystemFallback,
            Func<Task<CaptureResult>> copy, Func<Task<CaptureResult>> system, Func<bool> stillTarget)
        {
            var first = await (copyFirst ? copy() : system());
            if (first.Success || !first.CanFallback || (copyFirst && !allowSystemFallback)) return first;
            if (!stillTarget())
                return new CaptureResult { Error = "取词期间切换了窗口，已取消。", Method = first.Method, ClipboardNote = first.ClipboardNote };
            var second = await (copyFirst ? system() : copy());
            second.Method = first.Method + " → " + second.Method;
            if (copyFirst) second.ClipboardNote = first.ClipboardNote ?? "剪贴板未被本工具修改";
            if (!second.Success && second.CanFallback)
                second.Error = copyFirst ? first.Error : second.Error;
            return second;
        }

        internal static bool UseCopyFirst(string process, string mode)
        {
            if (mode == "copy") return true;
            if (mode == "system") return false;
            return string.Equals(process, "chrome", StringComparison.OrdinalIgnoreCase)
                || string.Equals(process, "zotero", StringComparison.OrdinalIgnoreCase)
                || string.Equals(process, "msedge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(process, "firefox", StringComparison.OrdinalIgnoreCase);
        }
    }
}
