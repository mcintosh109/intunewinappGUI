using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace IntuneWinPackager
{
    /// <summary>
    /// Runs IntuneWinAppUtil.exe for a single setup file.
    /// Command line: IntuneWinAppUtil.exe -c &lt;setup folder&gt; -s &lt;setup file&gt; -o &lt;output folder&gt; -q
    /// </summary>
    static class IntuneWinAppUtilRunner
    {
        public const string ToolFileName = "IntuneWinAppUtil.exe";
        public const string DownloadUrl = "https://github.com/microsoft/Microsoft-Win32-Content-Prep-Tool";

        /// <summary>
        /// Looks for IntuneWinAppUtil.exe next to this program, in the input folder, or on the PATH.
        /// </summary>
        public static string FindTool(params string[] extraFolders)
        {
            var candidates = new System.Collections.Generic.List<string> { AppDomain.CurrentDomain.BaseDirectory };
            candidates.AddRange(extraFolders);
            candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator));

            foreach (string folder in candidates)
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;
                try
                {
                    string path = Path.Combine(folder.Trim(), ToolFileName);
                    if (File.Exists(path))
                        return path;
                }
                catch (ArgumentException)
                {
                    // Invalid characters in a PATH entry, skip it.
                }
            }
            return null;
        }

        /// <summary>
        /// Packages one setup file. Returns the path of the created .intunewin file, or null on failure.
        /// </summary>
        public static async Task<string> PackageAsync(string toolPath, SetupFile setup, string outputFolder, Action<string> log)
        {
            Directory.CreateDirectory(outputFolder);

            string expectedOutput = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(setup.FileName) + ".intunewin");
            DateTime startedUtc = DateTime.UtcNow;

            var psi = new ProcessStartInfo
            {
                FileName = toolPath,
                Arguments = string.Format("-c {0} -s {1} -o {2} -q",
                    Quote(setup.SourceFolder), Quote(setup.FullPath), Quote(outputFolder)),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = setup.SourceFolder
            };

            log("> " + Path.GetFileName(toolPath) + " " + psi.Arguments);

            int exitCode = await Task.Run(() =>
            {
                using (var process = new Process { StartInfo = psi })
                {
                    process.OutputDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log("  " + e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) log("  [error] " + e.Data); };
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    return process.ExitCode;
                }
            });

            // The tool does not always return a non-zero exit code on failure, so also check the file was written.
            bool created = File.Exists(expectedOutput) && File.GetLastWriteTimeUtc(expectedOutput) >= startedUtc.AddSeconds(-2);
            if (exitCode != 0 || !created)
            {
                log(string.Format("  FAILED (exit code {0}{1})", exitCode, created ? "" : ", no .intunewin file was created"));
                return null;
            }
            return expectedOutput;
        }

        static string Quote(string path)
        {
            // A trailing backslash would escape the closing quote ("C:\Apps\" -> C:\Apps"), so end it with "." instead.
            if (path.EndsWith("\\"))
                path += ".";
            return "\"" + path + "\"";
        }
    }
}
