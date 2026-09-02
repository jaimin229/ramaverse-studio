using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RamaverseStudio.Services
{
    /// <summary>
    /// One-click FFmpeg provisioning. Downloads the official gyan.dev build
    /// (~80 MB), extracts only ffmpeg.exe into the app folder and makes the
    /// resolver see it — turning the #1 onboarding blocker into a 2-minute
    /// guided fix that needs zero technical knowledge.
    /// </summary>
    public static class FFmpegSetupService
    {
        // Stable release-full builds from the de-facto Windows FFmpeg distribution.
        private const string DownloadUrl =
            "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        public enum SetupResult
        {
            AlreadyInstalled,
            DownloadedAndInstalled,
            FailedNetwork,
            FailedExtract,
            FailedUnknown
        }

        public static event Action<string>? ProgressMessage;

        /// <summary>
        /// Downloads and installs FFmpeg.exe next to the app. Reports human
        /// progress via the optional callback and the numeric progress reporter.
        /// </summary>
        public static async Task<SetupResult> InstallFFmpegAsync(
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            try
            {
                if (Output.FFmpegPathResolver.IsAvailable)
                {
                    return SetupResult.AlreadyInstalled;
                }

                string appDir = AppDomain.CurrentDomain.BaseDirectory;

                // Some installs (Program Files, OneDrive) are not writable; fall
                // back to a per-user tools directory the resolver also checks.
                string targetDir = appDir;
                string targetExe = Path.Combine(targetDir, "ffmpeg.exe");
                try
                {
                    Directory.CreateDirectory(targetDir);
                    using var probe = File.Create(Path.Combine(targetDir, ".write_test"), 1, FileOptions.DeleteOnClose);
                }
                catch
                {
                    targetDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "RamaverseStudio", "ffmpeg");
                    Directory.CreateDirectory(targetDir);
                    targetExe = Path.Combine(targetDir, "ffmpeg.exe");
                }

                // Already downloaded on a previous run?
                if (File.Exists(targetExe) && VerifyRuns(targetExe))
                {
                    Report(progress, 100);
                    return SetupResult.DownloadedAndInstalled;
                }

                string zipPath = Path.Combine(Path.GetTempPath(), "ramaverse_ffmpeg.zip");

                ProgressMessage?.Invoke("Downloading FFmpeg (~80 MB, one time only)...");
                using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
                using (var resp = await http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    long total = resp.Content.Headers.ContentLength ?? -1;

                    await using (var src = await resp.Content.ReadAsStreamAsync(ct))
                    await using (var dst = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        byte[] buf = new byte[8192];
                        long read = 0;
                        int n;
                        while ((n = await src.ReadAsync(buf, ct)) > 0)
                        {
                            await dst.WriteAsync(buf.AsMemory(0, n), ct);
                            read += n;
                            if (total > 0)
                            {
                                double pct = read * 90.0 / total; // 0-90% for download
                                progress?.Report(pct);
                                if (read % (8192 * 512) == 0)
                                {
                                    ProgressMessage?.Invoke($"Downloading FFmpeg... {pct:F0}%");
                                }
                            }
                        }
                    }
                }

                ProgressMessage?.Invoke("Extracting ffmpeg.exe...");
                progress?.Report(92);

                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    var entry = FindFfmpegEntry(zip);
                    if (entry == null)
                    {
                        return SetupResult.FailedExtract;
                    }
                    entry.ExtractToFile(targetExe, true);
                }

                progress?.Report(98);
                ProgressMessage?.Invoke("Verifying...");
                if (!VerifyRuns(targetExe))
                {
                    try { File.Delete(targetExe); } catch { }
                    return SetupResult.FailedExtract;
                }

                try { File.Delete(zipPath); } catch { }
                progress?.Report(100);
                ProgressMessage?.Invoke("FFmpeg installed. Recording unlocked!");
                return SetupResult.DownloadedAndInstalled;
            }
            catch (OperationCanceledException)
            {
                return SetupResult.FailedNetwork;
            }
            catch (HttpRequestException)
            {
                return SetupResult.FailedNetwork;
            }
            catch (Exception)
            {
                return SetupResult.FailedUnknown;
            }
        }

        private static ZipArchiveEntry? FindFfmpegEntry(ZipArchive zip)
        {
            foreach (var e in zip.Entries)
            {
                if (e.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return e;
                }
            }
            return null;
        }

        private static bool VerifyRuns(string exePath)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                if (p == null) return false;
                string firstLine = p.StandardOutput.ReadLine() ?? "";
                p.WaitForExit(5000);
                return firstLine.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void Report(IProgress<double>? progress, double value) =>
            progress?.Report(value);
    }
}
