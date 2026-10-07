using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Shici
{
    public sealed class WindowGeometry
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public bool HasPosition { get; set; }
        public bool Maximized { get; set; }
        internal void Sanitize(bool compact)
        {
            Width = ValidSize(Width, compact ? 340 : 600, compact ? 420 : 770);
            Height = ValidSize(Height, compact ? 340 : 520, compact ? 380 : 685);
            if (Math.Abs((long)Left) > 100000 || Math.Abs((long)Top) > 100000) HasPosition = false;
        }
        private static double ValidSize(double value, double minimum, double fallback)
        {
            return double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > 10000 ? fallback : value;
        }
    }

    public sealed class WindowPreferences
    {
        public bool Compact { get; set; }
        public bool FixedPosition { get; set; }
        public bool AlwaysOnTop { get; set; }
        public WindowGeometry Normal { get; set; }
        public WindowGeometry Small { get; set; }
        public WindowPreferences()
        {
            Normal = new WindowGeometry { Width = 770, Height = 685 };
            Small = new WindowGeometry { Width = 420, Height = 380 };
        }
        internal WindowGeometry Current { get { return Compact ? Small : Normal; } }
        internal void Sanitize()
        {
            Normal = Normal ?? new WindowGeometry();
            Small = Small ?? new WindowGeometry();
            Normal.Sanitize(false); Small.Sanitize(true);
        }
        internal bool FollowCursor(bool maximized) { return !FixedPosition && !maximized; }
        internal static int ClampPosition(int proposed, int extent, int workStart, int workEnd)
        {
            return Math.Max(workStart, Math.Min(proposed, workEnd - extent));
        }
    }

    internal sealed class WindowPreferencesStore
    {
        private readonly string path;
        internal WindowPreferencesStore() : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shici", "window.json")) { }
        internal WindowPreferencesStore(string path) { this.path = path; }
        internal WindowPreferences Load()
        {
            try
            {
                if (!File.Exists(path) || new FileInfo(path).Length > 16384) return new WindowPreferences();
                var value = new JavaScriptSerializer().Deserialize<WindowPreferences>(File.ReadAllText(path, Encoding.UTF8));
                if (value == null) return new WindowPreferences();
                value.Sanitize();
                return value;
            }
            catch { return new WindowPreferences(); }
        }
        internal void Save(WindowPreferences value)
        {
            value.Sanitize();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(value), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
    }
}
