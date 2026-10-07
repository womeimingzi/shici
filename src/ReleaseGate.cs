using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Shici
{
    internal static class ReleaseGate
    {
        internal static Task<string> Wait(IntPtr target)
        {
            var clock = Stopwatch.StartNew();
            return Wait(Native.CaptureKeysDown, () => Native.GetForegroundWindow() == target,
                () => clock.ElapsedMilliseconds, milliseconds => Task.Delay(milliseconds));
        }

        internal static async Task<string> Wait(Func<bool> keysDown, Func<bool> stillTarget,
            Func<long> elapsed, Func<int, Task> delay)
        {
            long releasedAt = -1;
            while (elapsed() < 1500)
            {
                if (!stillTarget()) return "取词期间切换了窗口，已取消。请在目标文本处重新触发。";
                long now = elapsed();
                if (keysDown()) releasedAt = -1;
                else if (releasedAt < 0) releasedAt = now;
                // Allow the foreground app to process key-up events before injecting Ctrl+C.
                else if (now - releasedAt >= 60) return null;
                await delay(15);
            }
            return "请松开 Alt 和 E 后再试。";
        }
    }
}
