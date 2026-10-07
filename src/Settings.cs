using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace Shici
{
    public sealed class TranslatorSettings
    {
        public string BaseUrl { get; set; }
        public string Model { get; set; }
        public string Protocol { get; set; }
        public string TargetLanguage { get; set; }
        public string CaptureMode { get; set; }
        public bool AutoTranslate { get; set; }
        public bool PrepareText { get; set; }
        public string ProtectedKey { get; set; }
        [ScriptIgnore] public string ApiKey { get; set; }
        public TranslatorSettings()
        {
            BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
            Model = "qwen-mt-flash"; Protocol = "qwen-mt"; TargetLanguage = "Chinese";
            CaptureMode = "auto"; AutoTranslate = true; PrepareText = true; ApiKey = "";
        }
        internal TranslatorSettings Clone() { return (TranslatorSettings)MemberwiseClone(); }
        internal bool Ready { get { return !string.IsNullOrWhiteSpace(ApiKey); } }

        internal Uri Endpoint()
        {
            Uri uri;
            if (!Uri.TryCreate((BaseUrl ?? "").Trim().TrimEnd('/') + "/", UriKind.Absolute, out uri)
                || (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) && !(uri.Scheme == "http" && uri.IsLoopback))
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("接口地址须为 HTTPS Base URL；本机服务可用 http://localhost。不要填写密钥、查询参数或 /chat/completions。");
            if (uri.AbsolutePath.TrimEnd('/').EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return new Uri(uri.AbsoluteUri.TrimEnd('/'));
            return new Uri(uri, "chat/completions");
        }
        internal void Validate(bool requireKey)
        {
            Endpoint();
            if (string.IsNullOrWhiteSpace(Model) || Model.Length > 160 || Model.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new InvalidOperationException("请填写有效的模型名称。");
            if (requireKey && !Ready) throw new InvalidOperationException("请先在模型设置中填写 API Key。");
            if ((ApiKey ?? "").IndexOfAny(new[] { '\r', '\n' }) >= 0) throw new InvalidOperationException("API Key 中不能包含换行。");
            if (Protocol != "qwen-mt" && Protocol != "deepseek" && Protocol != "openai" && Protocol != "compatible")
                throw new InvalidOperationException("不支持的模型协议。");
            if (TargetLanguage != "Chinese" && TargetLanguage != "English" && TargetLanguage != "Japanese")
                throw new InvalidOperationException("不支持的目标语言。");
            if (CaptureMode != "auto" && CaptureMode != "copy" && CaptureMode != "system") CaptureMode = "auto";
        }
    }

    internal sealed class SettingsStore
    {
        private readonly string path;
        internal SettingsStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shici", "settings.json")) { }
        internal SettingsStore(string path) { this.path = path; }
        internal TranslatorSettings Load(out string notice)
        {
            notice = null;
            if (!File.Exists(path)) return new TranslatorSettings();
            try
            {
                if (new FileInfo(path).Length > 65536) throw new InvalidDataException();
                var value = new JavaScriptSerializer().Deserialize<TranslatorSettings>(File.ReadAllText(path, Encoding.UTF8));
                if (value == null) throw new InvalidDataException();
                value.Validate(false);
                if (!string.IsNullOrEmpty(value.ProtectedKey))
                    value.ApiKey = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value.ProtectedKey), null, DataProtectionScope.CurrentUser));
                return value;
            }
            catch
            {
                notice = "设置无法读取或密钥不属于当前 Windows 账户，请打开模型设置重新填写。";
                return new TranslatorSettings();
            }
        }
        internal void Save(TranslatorSettings settings)
        {
            settings.Validate(false);
            var saved = settings.Clone();
            saved.ProtectedKey = settings.Ready ? Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(settings.ApiKey), null, DataProtectionScope.CurrentUser)) : null;
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(saved), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
    }

    internal sealed class ModelPreset
    {
        internal string Name, Model, BaseUrl, Protocol, Hint;
        public override string ToString() { return Name; }
        internal static readonly ModelPreset[] All = {
            new ModelPreset { Name = "Qwen-MT-Flash · 推荐", Model = "qwen-mt-flash", BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1", Protocol = "qwen-mt", Hint = "专用翻译模型。北京地域输入 ¥0.7 / 输出 ¥1.95 每百万 token；API Key 需匹配地域。" },
            new ModelPreset { Name = "Qwen-MT-Plus · 质量优先", Model = "qwen-mt-plus", BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1", Protocol = "qwen-mt", Hint = "专用翻译模型。北京地域输入 ¥1.8 / 输出 ¥5.4 每百万 token。" },
            new ModelPreset { Name = "DeepSeek Flash · 非思考", Model = "deepseek-flash", BaseUrl = "https://api.deepseek.com/v1", Protocol = "deepseek", Hint = "通用模型，关闭思考。输入 $0.15–0.30 / 输出 $0.60–1.20 每百万 token，依峰谷时段变化。" },
            new ModelPreset { Name = "OpenAI GPT-6 Luna · 非思考", Model = "gpt-6-luna", BaseUrl = "https://api.openai.com/v1", Protocol = "openai", Hint = "通用轻量模型，关闭思考。Standard 短上下文输入 $0.10 / 输出 $0.50 每百万 token。" },
            new ModelPreset { Name = "自定义兼容接口", Model = "", BaseUrl = "", Protocol = "compatible", Hint = "填写支持 Chat Completions 流式输出的 Base URL 和模型名称。不同服务商的参数支持可能不同。" }
        };
    }
}
