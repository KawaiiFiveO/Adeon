using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MinimalChessEngine
{
    public static class StyleManager
    {
        private static readonly Dictionary<string, PlayStyle> _styles = new Dictionary<string, PlayStyle>(StringComparer.OrdinalIgnoreCase);

        public static void LoadStyles()
        {
            _styles.Clear();
            string exePath = Process.GetCurrentProcess().MainModule.FileName;
            string exeDir = Path.GetDirectoryName(exePath);
            string styleFile = Path.Combine(exeDir, "styles.json");

            //Uci.Log($"Looking for styles.json at: {styleFile}");
            try
            {
                if (!File.Exists(styleFile))
                {
                    Uci.Log("styles.json not found. Creating a default file.");
                    var defaultStyles = new Dictionary<string, PlayStyle> { { "Normal", new PlayStyle() } };
                    var options = new JsonSerializerOptions { WriteIndented = true };

                    // Use the new context for serialization
                    string defaultJson = JsonSerializer.Serialize(defaultStyles, typeof(Dictionary<string, PlayStyle>), JsonContext.Default);
                    File.WriteAllText(styleFile, defaultJson);
                }
                Uci.Log("Styles loaded successfully.");
                string json = File.ReadAllText(styleFile);
                // Use the new context for deserialization
                var loadedStyles = JsonSerializer.Deserialize(json, typeof(Dictionary<string, PlayStyle>), JsonContext.Default) as Dictionary<string, PlayStyle>;

                if (loadedStyles != null)
                {
                    foreach (var kvp in loadedStyles)
                    {
                        _styles[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Uci.Log($"Error loading styles.json: {ex.Message}. Using default style only.");
                if (_styles.Count == 0)
                {
                    _styles.Add("Normal", new PlayStyle());
                }
            }
        }

        public static PlayStyle GetStyle(string uciName)
        {
            if (_styles.TryGetValue(uciName, out PlayStyle style))
            {
                return style;
            }
            return _styles.TryGetValue("Normal", out var normalStyle) ? normalStyle : new PlayStyle();
        }

        public static List<string> GetStyleUciNames()
        {
            return _styles.Keys.ToList();
        }
    }
}