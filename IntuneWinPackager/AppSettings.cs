using System;
using System.Collections.Generic;
using System.IO;

namespace IntuneWinPackager
{
    /// <summary>
    /// Remembers the last used folders in %APPDATA%\IntuneWinPackager\settings.txt.
    /// </summary>
    class AppSettings
    {
        public string ToolPath { get; set; } = string.Empty;
        public string InputFolder { get; set; } = string.Empty;
        public string OutputFolder { get; set; } = string.Empty;
        public bool IncludeSubfolders { get; set; } = true;

        static string SettingsFile
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IntuneWinPackager", "settings.txt");
            }
        }

        public static AppSettings Load()
        {
            var settings = new AppSettings();
            try
            {
                if (!File.Exists(SettingsFile))
                    return settings;

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(SettingsFile))
                {
                    int split = line.IndexOf('=');
                    if (split > 0)
                        values[line.Substring(0, split)] = line.Substring(split + 1);
                }

                string value;
                if (values.TryGetValue("ToolPath", out value)) settings.ToolPath = value;
                if (values.TryGetValue("InputFolder", out value)) settings.InputFolder = value;
                if (values.TryGetValue("OutputFolder", out value)) settings.OutputFolder = value;
                if (values.TryGetValue("IncludeSubfolders", out value)) settings.IncludeSubfolders = value != "False";
            }
            catch (IOException)
            {
                // Fall back to defaults if the file can't be read.
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                File.WriteAllLines(SettingsFile, new[]
                {
                    "ToolPath=" + ToolPath,
                    "InputFolder=" + InputFolder,
                    "OutputFolder=" + OutputFolder,
                    "IncludeSubfolders=" + IncludeSubfolders
                });
            }
            catch (IOException)
            {
                // Not being able to save settings shouldn't stop the program.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
