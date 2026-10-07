using System;

namespace Shici
{
    internal sealed class CaptureResult
    {
        internal string Text, Error, Method, ClipboardNote, Source;
        internal long Milliseconds;
        // Only an unavailable provider or an empty copy may use another source.
        // Cancellation, changed ownership and protected content must stop the capture.
        internal bool CanFallback;
        internal bool Success { get { return !string.IsNullOrWhiteSpace(Text) && string.IsNullOrEmpty(Error); } }
    }
}
