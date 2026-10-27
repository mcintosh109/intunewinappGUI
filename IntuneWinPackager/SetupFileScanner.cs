using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IntuneWinPackager
{
    /// <summary>
    /// A setup file found in the input folder that can be packaged with IntuneWinAppUtil.exe.
    /// </summary>
    class SetupFile
    {
        public string FullPath { get; set; }

        /// <summary>Path relative to the input folder, e.g. "7-Zip\7z2301-x64.msi".</summary>
        public string RelativePath { get; set; }

        /// <summary>Folder that gets packaged (-c). This is the folder the setup file lives in.</summary>
        public string SourceFolder { get; set; }

        /// <summary>Folder relative to the input root, empty when the file sits directly in the root.</summary>
        public string RelativeFolder { get; set; }

        public string FileName { get { return Path.GetFileName(FullPath); } }

        public string Extension { get { return Path.GetExtension(FullPath).TrimStart('.').ToUpperInvariant(); } }

        public long SizeBytes { get; set; }
    }

    static class SetupFileScanner
    {
        public static readonly string[] SetupExtensions = { ".msi", ".exe", ".ps1", ".cmd", ".bat" };

        public static List<SetupFile> Scan(string inputFolder, bool includeSubfolders)
        {
            string root = Path.GetFullPath(inputFolder).TrimEnd(Path.DirectorySeparatorChar);
            var option = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            return Directory.EnumerateFiles(root, "*.*", option)
                .Where(f => SetupExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .Select(f =>
                {
                    string folder = Path.GetDirectoryName(f);
                    return new SetupFile
                    {
                        FullPath = f,
                        RelativePath = f.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar),
                        SourceFolder = folder,
                        RelativeFolder = folder.Length > root.Length
                            ? folder.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar)
                            : string.Empty,
                        SizeBytes = new FileInfo(f).Length
                    };
                })
                .OrderBy(s => s.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return string.Format("{0:0.#} {1}", size, units[unit]);
        }
    }
}
