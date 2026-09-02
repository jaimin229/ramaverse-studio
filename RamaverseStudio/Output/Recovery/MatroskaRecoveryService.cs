using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RamaverseStudio.Output.Recovery
{
    public record RecoveryReport(
        bool Success,
        string InputPath,
        string OutputPath,
        long RepairedFileSizeBytes,
        string EngineUsed,
        string DiagnosticLogs
    );

    /// <summary>
    /// Automated Matroska EBML Recovery & Error-Tolerant Remux Service.
    /// Rebuilds missing Cue seek tables and finalizes interrupted recordings without loss.
    /// </summary>
    public class MatroskaRecoveryService
    {
        private readonly string _ffmpegPath;
        private readonly string _mkvmergePath;

        public MatroskaRecoveryService(string ffmpegPath, string mkvmergePath = "")
        {
            _ffmpegPath = ffmpegPath;
            _mkvmergePath = mkvmergePath;
        }

        public bool IsValidMatroskaHeader(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] header = new byte[4];
                int bytesRead = fs.Read(header, 0, 4);
                if (bytesRead < 4) return false;

                // Matroska EBML Header ID: 0x1A 0x45 0xDF 0xA3
                return header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3;
            }
            catch
            {
                return false;
            }
        }

        public async Task<RecoveryReport> RepairCorruptedMkvAsync(
            string inputMkvPath,
            string? outputFilePath = null,
            bool remuxToMp4 = false,
            CancellationToken ct = default)
        {
            if (!File.Exists(inputMkvPath))
                throw new FileNotFoundException("Target unfinalized MKV file not found", inputMkvPath);

            string targetExt = remuxToMp4 ? ".mp4" : ".mkv";
            string output = outputFilePath ?? Path.Combine(
                Path.GetDirectoryName(inputMkvPath)!,
                $"{Path.GetFileNameWithoutExtension(inputMkvPath)}_Recovered{targetExt}"
            );

            if (!remuxToMp4 && !string.IsNullOrEmpty(_mkvmergePath) && File.Exists(_mkvmergePath))
            {
                var mkvmergeResult = await ExecuteMkvmergeIndexRebuildAsync(inputMkvPath, output, ct);
                if (mkvmergeResult.Success) return mkvmergeResult;
            }

            return await ExecuteFfmpegLosslessRemuxAsync(inputMkvPath, output, remuxToMp4, ct);
        }

        private async Task<RecoveryReport> ExecuteMkvmergeIndexRebuildAsync(string input, string output, CancellationToken ct)
        {
            var logBuilder = new StringBuilder();
            var psi = new ProcessStartInfo
            {
                FileName = _mkvmergePath,
                Arguments = $"-o \"{output}\" --clusters-in-meta-seek \"{input}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) logBuilder.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) logBuilder.AppendLine(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            bool success = proc.ExitCode == 0 && File.Exists(output) && new FileInfo(output).Length > 1024;
            long size = success ? new FileInfo(output).Length : 0;

            return new RecoveryReport(success, input, output, size, "mkvmerge-cluster-reindex", logBuilder.ToString());
        }

        private async Task<RecoveryReport> ExecuteFfmpegLosslessRemuxAsync(string input, string output, bool targetMp4, CancellationToken ct)
        {
            var logBuilder = new StringBuilder();
            string movFlags = targetMp4 ? "-movflags +faststart" : "";
            string args = $"-y -err_detect ignore_err -fflags +genpts+discardcorrupt -i \"{input}\" -c copy {movFlags} \"{output}\"";

            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) logBuilder.AppendLine(e.Data); };
            proc.Start();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            bool success = proc.ExitCode == 0 && File.Exists(output) && new FileInfo(output).Length > 1024;
            long size = success ? new FileInfo(output).Length : 0;

            return new RecoveryReport(success, input, output, size, targetMp4 ? "ffmpeg-streamcopy-mp4" : "ffmpeg-streamcopy-mkv", logBuilder.ToString());
        }
    }
}
