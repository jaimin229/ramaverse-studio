using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace RamaverseStudio.AutoUpdate
{
    public class UpdateInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0.0";

        [JsonPropertyName("releaseDate")]
        public string ReleaseDate { get; set; } = "";

        [JsonPropertyName("releaseNotes")]
        public string ReleaseNotes { get; set; } = "";

        [JsonPropertyName("downloadUrl")]
        public string DownloadUrl { get; set; } = "";

        [JsonPropertyName("mandatory")]
        public bool Mandatory { get; set; } = false;

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";
    }

    public class UpdateManager
    {
        public static readonly Version CurrentVersion = new Version(1, 0, 0);
        public const string DefaultUpdateUrl = "https://raw.githubusercontent.com/Jaimin-prajapati-ds/ramaverse-studio/main/update_manifest.json";

        private readonly HttpClient _httpClient;

        public UpdateManager()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RamaverseStudio-AutoUpdater/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<UpdateInfo?> CheckForUpdatesAsync(string? updateUrl = null)
        {
            string url = string.IsNullOrWhiteSpace(updateUrl) ? DefaultUpdateUrl : updateUrl;

            try
            {
                // In local/test environment, if URL is a local file path or mock
                if (File.Exists(url))
                {
                    string json = await File.ReadAllTextAsync(url);
                    var info = JsonSerializer.Deserialize<UpdateInfo>(json);
                    if (info != null && IsNewerVersion(info.Version))
                    {
                        return info;
                    }
                    return null;
                }

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return null;

                string content = await response.Content.ReadAsStringAsync();
                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(content);

                if (updateInfo != null && IsNewerVersion(updateInfo.Version))
                {
                    return updateInfo;
                }
            }
            catch (Exception)
            {
                // Graceful fallback if network is unreachable
            }

            return null;
        }

        public static bool IsNewerVersion(string remoteVersionStr)
        {
            if (Version.TryParse(remoteVersionStr.TrimStart('v', 'V'), out var remoteVer))
            {
                return remoteVer > CurrentVersion;
            }
            return false;
        }

        public async Task<string?> DownloadUpdateAsync(string downloadUrl, IProgress<double>? progress = null, CancellationToken ct = default)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "RamaverseStudioUpdate");
                Directory.CreateDirectory(tempDir);
                string zipPath = Path.Combine(tempDir, "update.zip");

                // If local file URL
                if (File.Exists(downloadUrl))
                {
                    File.Copy(downloadUrl, zipPath, true);
                    progress?.Report(100.0);
                    return zipPath;
                }

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                byte[] buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        double percent = (double)totalRead / totalBytes * 100.0;
                        progress?.Report(percent);
                    }
                }

                return zipPath;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void ApplyUpdateAndRestart(string zipFilePath)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Environment.ProcessPath ?? Path.Combine(appDir, "RamaverseStudio.exe");
            int currentPid = Process.GetCurrentProcess().Id;

            string tempExtractDir = Path.Combine(Path.GetTempPath(), "RamaverseExtracted_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempExtractDir);

            // Unzip package
            ZipFile.ExtractToDirectory(zipFilePath, tempExtractDir, true);

            // Create atomic update runner batch script
            string scriptPath = Path.Combine(Path.GetTempPath(), "ramaverse_update_runner.bat");
            string scriptContent = $@"@echo off
title Ramaverse Studio Updater
echo Waiting for Ramaverse Studio to exit...
:wait_loop
tasklist /FI ""PID eq {currentPid}"" 2>NUL | find /I ""{currentPid}"" >NUL
if not errorlevel 1 (
    timeout /t 1 /nobreak >NUL
    goto wait_loop
)

echo Applying updates to {appDir}...
xcopy /s /e /y ""{tempExtractDir}\*"" ""{appDir}""
timeout /t 1 /nobreak >NUL

echo Launching updated Ramaverse Studio...
start """" ""{exePath}""

echo Cleaning temporary files...
rmdir /s /q ""{tempExtractDir}""
del ""%~f0""
exit
";

            File.WriteAllText(scriptPath, scriptContent);

            // Execute script and terminate current process
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(psi);
            Environment.Exit(0);
        }
    }
}
