using System;
using System.IO;

namespace RamaverseStudio.Output
{
    public static class FFmpegPathResolver
    {
        private static string? _cachedPath = null;

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

            // 5. Default fallback to system executable alias
            _cachedPath = "ffmpeg";
            return "ffmpeg";
        }
    }
}
