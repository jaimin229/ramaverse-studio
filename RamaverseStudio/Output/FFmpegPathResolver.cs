using System;
using System.IO;
using System.Security;

namespace RamaverseStudio.Output
{
    public static class FFmpegPathResolver
    {
        private static string? _cachedPath = null;

        /// <summary>
        /// Clears the cached path so the next GetFFmpegPath re-scans — used after
        /// the auto-installer drops a new ffmpeg.exe into a search location.
        /// </summary>
        public static void InvalidateCache() => _cachedPath = null;

        /// <summary>
        /// True when a real, existing ffmpeg.exe was located (not the bare PATH alias).
        /// </summary>
        public static bool IsAvailable
        {
            get
            {
                string path = GetFFmpegPath();
                return !string.Equals(path, "ffmpeg", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Returns a sanitized, user-safe error message when FFmpeg is missing.
        /// </summary>
        public static string GetMissingFfmpegHelpMessage()
        {
            return "FFmpeg was not found on this computer.\n\n" +
                   "Ramaverse Studio needs FFmpeg to record and stream video.\n\n" +
                   "HOW TO FIX (takes 1 minute):\n" +
                   "  1. Download FFmpeg from https://www.gyan.dev/ffmpeg/builds/ (ffmpeg-release-full)\n" +
                   "  2. Unzip it, then either:\n" +
                   "     - Copy ffmpeg.exe into the Ramaverse Studio folder, OR\n" +
                   "     - Add the ffmpeg\\bin folder to your Windows PATH\n" +
                   "  3. Restart Ramaverse Studio and try again.";
        }

        public static string GetFFmpegPath()
        {
            if (_cachedPath != null && File.Exists(_cachedPath))
            {
                return _cachedPath;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 1. Check direct app folder
            string directAppExe = Path.Combine(baseDir, "ffmpeg.exe");
            if (File.Exists(directAppExe))
            {
                _cachedPath = directAppExe;
                return directAppExe;
            }

            // 2. Check ./ffmpeg/bin/ffmpeg.exe
            string subBinExe = Path.Combine(baseDir, "ffmpeg", "bin", "ffmpeg.exe");
            if (File.Exists(subBinExe))
            {
                _cachedPath = subBinExe;
                return subBinExe;
            }

            // 3. Check ./ffmpeg/ffmpeg.exe
            string subExe = Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe");
            if (File.Exists(subExe))
            {
                _cachedPath = subExe;
                return subExe;
            }

            // 4. Check system PATH directories
            try
            {
                string? pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrWhiteSpace(pathEnv))
                {
                    var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var p in paths)
                    {
                        try
                        {
                            string candidate = Path.Combine(p.Trim(), "ffmpeg.exe");
                            if (File.Exists(candidate))
                            {
                                _cachedPath = candidate;
                                return candidate;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            // 5. Per-user tools folder (writable fallback used by the auto-installer)
            string userTools = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RamaverseStudio", "ffmpeg", "ffmpeg.exe");
            try
            {
                if (File.Exists(userTools))
                {
                    _cachedPath = userTools;
                    return userTools;
                }
            }
            catch { }

            // 6. Common winget/scoop install locations as a last resort
            string[] commonRoots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Links"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "shims"),
                @"C:\ffmpeg\bin"
            };
            foreach (var root in commonRoots)
            {
                try
                {
                    string candidate = Path.Combine(root, "ffmpeg.exe");
                    if (File.Exists(candidate))
                    {
                        _cachedPath = candidate;
                        return candidate;
                    }
                }
                catch { }
            }

            // 6. Default fallback to system executable alias
            _cachedPath = "ffmpeg";
            return "ffmpeg";
        }

        /// <summary>
        /// True when a real, existing ffmpeg.exe was located (not the bare PATH alias).
        /// </summary>
        public static bool TryGetRealPath(out string path)
        {
            path = GetFFmpegPath();
            return !string.Equals(path, "ffmpeg", StringComparison.OrdinalIgnoreCase);
        }
    }
}
