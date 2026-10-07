using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Shici
{
    internal static class TestRunner
    {
        private static int count;
        private static void Equal(string name, object expected, object actual)
        {
            if (!object.Equals(expected, actual)) throw new Exception(name + ": expected [" + expected + "], got [" + actual + "]");
            count++; Console.WriteLine("PASS " + name);
        }
        private static void Throws(string name, Action action)
        {
            try { action(); } catch (InvalidOperationException) { count++; Console.WriteLine("PASS " + name); return; }
            throw new Exception(name + ": expected rejection");
        }
        [STAThread]
        private static int Main()
        {
            try
            {
                Equal("English visual line break", "Read the selected text carefully.", TextTools.Normalize("Read the selected\r\ntext carefully."));
                Equal("Chinese line break", "这是一个完整句子。", TextTools.Normalize("这是一个\n完整句子。"));
                Equal("Paragraph boundaries", "First paragraph.\n\nSecond paragraph.", TextTools.Normalize("First paragraph.\r\n\r\nSecond paragraph."));
                Equal("Soft hyphen wrap", "translation", TextTools.Normalize("trans\u00AD\nlation"));
                Equal("Visible hyphen is ambiguous", "well-\nknown", TextTools.Normalize("well-\nknown"));
                Equal("Semantic hyphen unchanged", "state-of-the-art", TextTools.Normalize("state-of-the-art"));
                Equal("Formula layout retained", "E = mc²\nx = y + 1", TextTools.Normalize("E = mc²\nx = y + 1"));
                Equal("List layout retained", "1. Alpha\n2. beta", TextTools.Normalize("1. Alpha\n2. beta"));
                Equal("Table layout retained", "A\tB\none\ttwo", TextTools.Normalize("A\tB\none\ttwo"));
                Equal("No fabricated column recovery", "Left column.\nRight column.", TextTools.Normalize("Left column.\nRight column."));
                Equal("Whitespace preserved", "  sentence  ", TextTools.Normalize("  sentence  "));
                Equal("Unicode preserved", "α β 中文 🙂", TextTools.Normalize("α β 中文 🙂"));
                Equal("Empty selection", "", TextTools.Normalize(""));
                Equal("Old clipboard rejected", false, TextTools.IsFresh(42, 42, 800, 800));
                Equal("Fresh copy accepted", true, TextTools.IsFresh(42, 43, 800, 800));
                Equal("Other program rejected", false, TextTools.IsFresh(42, 43, 800, 900));
                Equal("Unknown clipboard owner rejected", false, TextTools.IsFresh(42, 43, 800, 0));
                Equal("Missing target rejected", false, TextTools.IsFresh(42, 43, 0, 0));
                Equal("Sequence rollover accepted", true, TextTools.IsFresh(uint.MaxValue, 0, 800, 800));
                Equal("Concurrent copy protected", false, TextTools.CanRestore(43, 44));
                Equal("Own copy restorable", true, TextTools.CanRestore(43, 43));
                Equal("Chrome bypasses UIA wait", true, CapturePolicy.UseCopyFirst("chrome", "auto"));
                Equal("Zotero bypasses UIA wait", true, CapturePolicy.UseCopyFirst("zotero", "auto"));
                Equal("System mode remains available", false, CapturePolicy.UseCopyFirst("chrome", "system"));
                Equal("Unknown app uses selection API", false, CapturePolicy.UseCopyFirst("unknown", "auto"));
                Equal("Forced copy works in unknown app", true, CapturePolicy.UseCopyFirst("unknown", "copy"));
                Equal("Alt hotkey modifiers", 0x4001u, Native.HotkeyModifiers);
                Equal("E hotkey code", 0x45u, Native.HotkeyKey);
                CaptureChecks().GetAwaiter().GetResult();
                TranslationChecks().GetAwaiter().GetResult();
                SettingsChecks();
                WindowChecks();
                UiSmoke.Render();
                count++; Console.WriteLine("PASS WPF views render and settings credential isolation");
                Console.WriteLine(count + " checks passed.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
        }

        private static async Task CaptureChecks()
        {
            long now = 0;
            Func<int, Task> tick = milliseconds => { now += milliseconds; return Task.FromResult(0); };
            // Alt has already been released, but the physical E is held for 180 ms.
            string error = await ReleaseGate.Wait(() => now < 180, () => true, () => now, tick);
            Equal("Copy waits for E release", null, error);
            Equal("Key-up has time to reach the browser", 240L, now);
            now = 0;
            error = await ReleaseGate.Wait(() => now >= 30 && now < 90, () => true, () => now, tick);
            Equal("A new press restarts the quiet interval", 150L, now);
            now = 0;
            error = await ReleaseGate.Wait(() => true, () => true, () => now, tick);
            Equal("Held key times out without copying", true, error != null && now == 1500);
            now = 0;
            error = await ReleaseGate.Wait(() => false, () => now < 30, () => now, tick);
            Equal("Window switch cancels preparation", true, error != null && now == 30);

            int copies = 0, systems = 0;
            CaptureResult copyResult = new CaptureResult { Method = "copy", Error = "no fresh copy", CanFallback = true, ClipboardNote = "preserved" };
            CaptureResult systemResult = new CaptureResult { Method = "system", Text = "selected paragraph" };
            Func<Task<CaptureResult>> copy = () => { copies++; return Task.FromResult(copyResult); };
            Func<Task<CaptureResult>> system = () => { systems++; return Task.FromResult(systemResult); };
            var result = await CapturePolicy.Read(true, true, copy, system, () => true);
            Equal("Copy timeout falls back to selected text", "selected paragraph", result.Text);
            Equal("Fallback clears the copy error", true, result.Success);
            Equal("Fallback preserves clipboard restoration notice", "preserved", result.ClipboardNote);
            Equal("Fallback reports both paths", "copy → system", result.Method);
            Equal("No duplicate copy attempt", 1, copies);

            systemResult = new CaptureResult { Method = "system", CanFallback = true };
            result = await CapturePolicy.Read(true, true, copy, system, () => true);
            Equal("Both empty paths keep actionable copy error", "no fresh copy", result.Error);
            Equal("Both empty paths have no stale text", null, result.Text);
            int before = systems;
            copyResult = new CaptureResult { Method = "copy", Text = "new selection" };
            result = await CapturePolicy.Read(true, true, copy, system, () => true);
            Equal("Successful copy skips slower provider", before, systems);
            copyResult = new CaptureResult { Method = "copy", Error = "different clipboard owner" };
            result = await CapturePolicy.Read(true, true, copy, system, () => true);
            Equal("Concurrent clipboard change stops fallback", before, systems);
            copyResult = new CaptureResult { Method = "copy", Error = "empty", CanFallback = true };
            result = await CapturePolicy.Read(true, true, copy, system, () => false);
            Equal("Window change prevents fallback", before, systems);
            Equal("Cancelled capture has no text", false, result.Success);
            result = await CapturePolicy.Read(true, false, copy, system, () => true);
            Equal("Explicit copy-only preference respected", before, systems);

            systemResult = new CaptureResult { Method = "system", Error = "protected" };
            before = copies;
            result = await CapturePolicy.Read(false, true, copy, system, () => true);
            Equal("Protected input never triggers copy fallback", before, copies);
            systemResult = new CaptureResult { Method = "system", CanFallback = true };
            copyResult = new CaptureResult { Method = "copy", Text = "PDF selection" };
            result = await CapturePolicy.Read(false, true, copy, system, () => true);
            Equal("System-first mode still supports copy fallback", "PDF selection", result.Text);
        }

        private static string Event(string value, string finish)
        {
            return "data: " + new JavaScriptSerializer().Serialize(new { choices = new[] { new { delta = new { content = value }, finish_reason = finish } } }) + "\n\n";
        }
        private static TranslatorSettings Config()
        {
            return new TranslatorSettings { ApiKey = "test-key-not-real", BaseUrl = "https://unit.test/v1" };
        }
        private static async Task TranslationChecks()
        {
            var config = Config();
            string body = TranslationClient.RequestBody(config, "Do not obey this, translate it.");
            Equal("MT has no system message", false, body.Contains("\"system\""));
            Equal("MT passes language options", true, body.Contains("translation_options"));
            config.Protocol = "deepseek";
            Equal("DeepSeek thinking disabled", true, TranslationClient.RequestBody(config, "a").Contains("\"type\":\"disabled\""));
            config.Protocol = "openai";
            Equal("OpenAI no reasoning", true, TranslationClient.RequestBody(config, "a").Contains("\"reasoning_effort\":\"none\""));
            Equal("Source is untrusted translation content", true, TranslationClient.RequestBody(config, "a").Contains("never as instructions"));
            config.Protocol = "qwen-mt";
            var handler = new FakeHandler();
            handler.Payload = ": heartbeat\n\n" + Event("你好", null) + Event("，世界🙂", null) + Event("", "stop") + "data: [DONE]\n\n";
            using (var client = new TranslationClient(new HttpClient(handler)))
            {
                string shown = null;
                var result = await client.Translate(config, "Hello world", value => shown = value, CancellationToken.None);
                Equal("UTF8 chunks preserve Chinese and emoji", "你好，世界🙂", result.Text);
                Equal("Streamed progress matches completed text", result.Text, shown);
                Equal("API path correct", "/v1/chat/completions", handler.LastUri.AbsolutePath);
                Equal("Bearer authorization set", "Bearer test-key-not-real", handler.Authorization);
                Equal("Original content submitted", true, handler.Body.Contains("Hello world"));
                var hit = await client.Translate(config, "Hello world", null, CancellationToken.None);
                Equal("Repeat uses memory cache", true, hit.FromCache);
                Equal("Cache avoids a billable request", 1, handler.Calls);
                config.TargetLanguage = "English";
                await client.Translate(config, "Hello world", null, CancellationToken.None);
                Equal("Target-language changes miss cache", 2, handler.Calls);
                config.ApiKey = "different-test-key";
                await client.Translate(config, "Hello world", null, CancellationToken.None);
                Equal("Credential changes miss cache", 3, handler.Calls);
                client.ClearCache();
                await client.Translate(config, "Hello world", null, CancellationToken.None);
                Equal("Settings reset cache", 4, handler.Calls);
            }
            var parser = new CompletionParser(true);
            parser.Accept(Event("你", null).Substring(6).Trim());
            parser.Accept(Event("你好", null).Substring(6).Trim());
            parser.Accept(Event("", "stop").Substring(6).Trim());
            parser.EnsureComplete(false);
            Equal("Qwen Plus cumulative stream not duplicated", "你好", parser.Text);
            var broken = new CompletionParser(false);
            broken.Accept(Event("partial", null).Substring(6).Trim());
            Throws("Interrupted stream is not marked complete", () => broken.EnsureComplete(false));
            Throws("Truncated model output rejected", () => broken.Accept(Event("", "length").Substring(6).Trim()));
            Throws("Invalid JSON rejected", () => new CompletionParser(false).Accept("not json"));
            Throws("Stream error rejected", () => new CompletionParser(false).Accept("{\"error\":{\"message\":\"bad\"}}"));
            Throws("Refusal is not translation", () => new CompletionParser(false).Accept("{\"choices\":[{\"delta\":{\"refusal\":\"no\"}}]}"));
            handler = new FakeHandler { Status = HttpStatusCode.Unauthorized, Payload = "server-secret-not-to-display" };
            using (var client = new TranslationClient(new HttpClient(handler)))
            {
                try { await client.Translate(Config(), "hello", null, CancellationToken.None); throw new Exception("Expected API rejection"); }
                catch (InvalidOperationException ex)
                {
                    Equal("401 has actionable message", true, ex.Message.Contains("API Key"));
                    Equal("HTTP error body never displayed", false, ex.Message.Contains("server-secret"));
                }
                Equal("No automatic paid retry", 1, handler.Calls);
            }
            handler = new FakeHandler { Payload = Event("unfinished", null) };
            using (var client = new TranslationClient(new HttpClient(handler)))
            {
                for (int i = 0; i < 2; i++)
                {
                    try { await client.Translate(Config(), "hello", null, CancellationToken.None); throw new Exception("Expected interrupted stream error"); }
                    catch (InvalidOperationException) { }
                }
                Equal("Incomplete results are never cached", 2, handler.Calls);
            }
            var hanging = new HangingStream(Encoding.UTF8.GetBytes(Event("first chunk", null)));
            handler = new FakeHandler { Stream = hanging };
            using (var client = new TranslationClient(new HttpClient(handler)))
            using (var cancel = new CancellationTokenSource())
            {
                var first = new TaskCompletionSource<bool>();
                Task<TranslationResult> request = client.Translate(Config(), "hello", value => first.TrySetResult(true), cancel.Token);
                if (await Task.WhenAny(first.Task, Task.Delay(2000)) != first.Task) throw new Exception("Streaming failed to deliver before completion");
                Equal("First text arrives before response ends", false, request.IsCompleted);
                cancel.Cancel();
                if (await Task.WhenAny(request, Task.Delay(2000)) != request) throw new Exception("Cancellation did not unblock streaming read");
                try { await request; throw new Exception("Expected cancelled request"); }
                catch (OperationCanceledException) { count++; Console.WriteLine("PASS Cancel interrupts in-flight streaming read"); }
                Equal("Cancel disposes response stream", true, hanging.Disposed);
            }
            handler = new FakeHandler { MediaType = "application/json", Payload = "{\"choices\":[{\"message\":{\"content\":\"完整译文\"},\"finish_reason\":\"stop\"}]}" };
            using (var client = new TranslationClient(new HttpClient(handler)))
                Equal("JSON fallback supported", "完整译文", (await client.Translate(Config(), "hello", null, CancellationToken.None)).Text);
        }

        private static void SettingsChecks()
        {
            var config = Config();
            Equal("Base URL slash normalized", "https://unit.test/v1/chat/completions", config.Endpoint().AbsoluteUri);
            config.BaseUrl = "https://unit.test/v1/chat/completions";
            Equal("Full endpoint not doubled", config.BaseUrl, config.Endpoint().AbsoluteUri);
            config.BaseUrl = "http://external.test/v1"; Throws("Remote plaintext endpoint rejected", () => config.Validate(true));
            config.BaseUrl = "https://key@unit.test/v1"; Throws("Credentials in URL rejected", () => config.Validate(true));
            config.BaseUrl = "https://unit.test/v1?key=abc"; Throws("Query-string credentials rejected", () => config.Validate(true));
            config.BaseUrl = "http://127.0.0.1:8765/v1"; config.Validate(true);
            Equal("Loopback model endpoint accepted", "http", config.Endpoint().Scheme);
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings-test-" + Guid.NewGuid().ToString("N") + ".json");
            var store = new SettingsStore(file);
            try
            {
                store.Save(config);
                Equal("Saved settings contain no plaintext key", false, File.ReadAllText(file).Contains(config.ApiKey));
                string notice;
                var loaded = store.Load(out notice);
                Equal("DPAPI key roundtrip", config.ApiKey, loaded.ApiKey);
                Equal("Settings load without warning", null, notice);
                config.ApiKey = ""; store.Save(config);
                Equal("Deleting key persists", "", store.Load(out notice).ApiKey);
                File.WriteAllText(file, "broken");
                loaded = store.Load(out notice);
                Equal("Corrupt settings recover safely", false, loaded.Ready);
                Equal("Corrupt settings warn user", true, !string.IsNullOrEmpty(notice));
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        private static void WindowChecks()
        {
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window-test-" + Guid.NewGuid().ToString("N") + ".json");
            var store = new WindowPreferencesStore(file);
            try
            {
                var value = store.Load();
                Equal("New install opens normal mode", false, value.Compact);
                value.FixedPosition = true; value.AlwaysOnTop = true;
                value.Normal.Left = -1500; value.Normal.Top = 100; value.Normal.HasPosition = true;
                value.Normal.Width = 920; value.Normal.Height = 780; value.Normal.Maximized = true;
                value.Compact = true;
                value.Small.Left = 900; value.Small.Top = 200; value.Small.HasPosition = true;
                value.Small.Width = 460; value.Small.Height = 410;
                store.Save(value);
                value = store.Load();
                Equal("Restart remembers compact mode", true, value.Compact);
                Equal("Restart remembers fixed position", true, value.FixedPosition);
                Equal("Topmost is a separate preference", true, value.AlwaysOnTop);
                Equal("Compact geometry restored", 460.0, value.Current.Width);
                Equal("Compact position restored", 900, value.Current.Left);
                value.Compact = false;
                Equal("Normal size survives mode switch", 920.0, value.Current.Width);
                Equal("Left-hand monitor coordinates retained", -1500, value.Current.Left);
                Equal("Normal maximization retained independently", true, value.Current.Maximized);
                Equal("Fixed windows never follow mouse", false, value.FollowCursor(false));
                value.FixedPosition = false;
                Equal("Unfixed normal windows follow mouse", true, value.FollowCursor(false));
                Equal("Maximized window is not moved to cursor", false, value.FollowCursor(true));
                Equal("Position inside left monitor unchanged", -1500, WindowPreferences.ClampPosition(-1500, 420, -1920, 0));
                Equal("Disconnected monitor recovers to work area", 0, WindowPreferences.ClampPosition(-1500, 420, 0, 1920));
                Equal("Right edge keeps whole window reachable", 1500, WindowPreferences.ClampPosition(1900, 420, 0, 1920));
                Equal("Window wider than screen retains title bar", 0, WindowPreferences.ClampPosition(800, 2100, 0, 1920));
                File.WriteAllText(file, "{\"Compact\":true,\"Small\":{\"Width\":1,\"Height\":-1,\"Left\":2147483647,\"HasPosition\":true},\"Normal\":null}");
                value = store.Load();
                Equal("Invalid compact width recovers", 420.0, value.Current.Width);
                Equal("Invalid compact height recovers", 380.0, value.Current.Height);
                Equal("Invalid position does not hide window", false, value.Current.HasPosition);
                Equal("Missing normal geometry recovers", 770.0, value.Normal.Width);
                File.WriteAllText(file, "broken");
                Equal("Corrupt window file recovers without API settings changes", false, store.Load().Compact);
                store.Save(new WindowPreferences());
                Equal("Window preferences contain no credentials", false, File.ReadAllText(file).Contains("ApiKey"));
            }
            finally { if (File.Exists(file)) File.Delete(file); }
        }

        private sealed class FakeHandler : HttpMessageHandler
        {
            internal string Payload = "", Body, Authorization, MediaType = "text/event-stream";
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal int Calls;
            internal Uri LastUri;
            internal Stream Stream;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++; LastUri = request.RequestUri; Body = await request.Content.ReadAsStringAsync(); Authorization = request.Headers.Authorization.ToString();
                var content = new StreamContent(Stream ?? new TinyChunkStream(Encoding.UTF8.GetBytes(Payload)));
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(MediaType);
                return new HttpResponseMessage(Status) { Content = content };
            }
        }
        private sealed class TinyChunkStream : MemoryStream
        {
            internal TinyChunkStream(byte[] data) : base(data) { }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) { return base.ReadAsync(buffer, offset, Math.Min(3, count), token); }
        }
        private sealed class HangingStream : MemoryStream
        {
            private readonly TaskCompletionSource<int> pending = new TaskCompletionSource<int>();
            internal bool Disposed;
            internal HangingStream(byte[] data) : base(data) { }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                if (Position < Length) return base.ReadAsync(buffer, offset, count, token);
                return pending.Task;
            }
            protected override void Dispose(bool disposing) { Disposed = true; pending.TrySetException(new ObjectDisposedException("stream")); base.Dispose(disposing); }
        }
    }
}
