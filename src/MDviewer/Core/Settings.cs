using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MDviewer.Core
{
    /// <summary>Persisted user preferences, stored as a flat key/value file.</summary>
    public sealed class Settings
    {
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Theme = "light";
        public double FontScale = 1.0;
        public bool SidebarVisible = true;
        public int SidebarWidth = 280;
        public int WindowX = int.MinValue;
        public int WindowY = int.MinValue;
        public int WindowWidth = 1120;
        public int WindowHeight = 760;
        public bool WindowMaximized;
        public string LastFolder = "";

        public static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MDviewer");
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                string path = SettingsPath;
                if (!File.Exists(path)) return s;

                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    s._values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }

                s.Theme = s.GetString("theme", s.Theme) == "dark" ? "dark" : "light";
                s.FontScale = s.GetDouble("fontScale", s.FontScale);
                s.SidebarVisible = s.GetBool("sidebarVisible", s.SidebarVisible);
                s.SidebarWidth = s.GetInt("sidebarWidth", s.SidebarWidth);
                s.WindowX = s.GetInt("windowX", s.WindowX);
                s.WindowY = s.GetInt("windowY", s.WindowY);
                s.WindowWidth = s.GetInt("windowWidth", s.WindowWidth);
                s.WindowHeight = s.GetInt("windowHeight", s.WindowHeight);
                s.WindowMaximized = s.GetBool("windowMaximized", s.WindowMaximized);
                s.LastFolder = s.GetString("lastFolder", s.LastFolder);
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                string path = SettingsPath;
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("# MDviewer settings");
                sb.AppendLine("theme=" + Theme);
                sb.AppendLine("fontScale=" + FontScale.ToString("0.###", CultureInfo.InvariantCulture));
                sb.AppendLine("sidebarVisible=" + (SidebarVisible ? "1" : "0"));
                sb.AppendLine("sidebarWidth=" + SidebarWidth.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("windowX=" + WindowX.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("windowY=" + WindowY.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("windowWidth=" + WindowWidth.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("windowHeight=" + WindowHeight.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("windowMaximized=" + (WindowMaximized ? "1" : "0"));
                sb.AppendLine("lastFolder=" + LastFolder);
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }

        private string GetString(string key, string fallback)
        {
            string v;
            return _values.TryGetValue(key, out v) && v.Length > 0 ? v : fallback;
        }

        private int GetInt(string key, int fallback)
        {
            string v;
            int n;
            if (_values.TryGetValue(key, out v) &&
                int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                return n;
            return fallback;
        }

        private bool GetBool(string key, bool fallback)
        {
            string v;
            if (!_values.TryGetValue(key, out v)) return fallback;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private double GetDouble(string key, double fallback)
        {
            string v;
            double d;
            if (_values.TryGetValue(key, out v) &&
                double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
            return fallback;
        }
    }
}
