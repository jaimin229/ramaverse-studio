using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using RamaverseStudio.Models;

namespace RamaverseStudio.Output
{
    public enum StreamHealthStatus
    {
        Offline,
        Connecting,
        Excellent,
        Good,
        Poor,
        Disconnected
    }

    public class StreamStats
    {
        public TimeSpan Uptime { get; set; }
        public double BitrateKbps { get; set; }
        public StreamHealthStatus Status { get; set; }
        public long DroppedFrames { get; set; }
    }

    public class FFmpegStreamingEngine : IDisposable
    {
        private Process? _ffmpegProcess;
        private Stream? _videoInputStream;
        private Stream? _audioInputStream;
        private NamedPipeServerStream? _audioPipeServer;
        private string _audioPipeName = "";

        private readonly object _stateLock = new object();
        private bool _isStreaming = false;
        private Stopwatch _stopwatch = new Stopwatch();

        public bool IsStreaming => _isStreaming;
        public StreamHealthStatus CurrentStatus { get; private set; } = StreamHealthStatus.Offline;

        public event Action<StreamStats>? StatsUpdated;

        public async Task<bool> StartStreamingAsync(StudioProfile profile)
        {
            lock (_stateLock)
            {
                if (_isStreaming) return false;
            }

            try
            {
                int width = profile.CanvasWidth;
                int height = profile.CanvasHeight;
                int fps = profile.Fps;
                int videoBitrate = profile.StreamBitrateKbps;
                int audioBitrate = profile.StreamAudioBitrateKbps;

                string encoder = FFmpegRecordingEngine.ResolveEncoderString(profile.Encoder);
                string rtmpTarget = profile.RtmpServerUrl.TrimEnd('/') + "/" + profile.StreamKey.Trim();

                if (string.IsNullOrWhiteSpace(profile.StreamKey))
                {
                    rtmpTarget = profile.RtmpServerUrl;
                }

                _audioPipeName = $"RamaverseStreamAudio_{Guid.NewGuid():N}";
                _audioPipeServer = new NamedPipeServerStream(_audioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);
                string pipePath = $@"\\.\pipe\{_audioPipeName}";

                // RTMP stream args
                string args = $"-y -f rawvideo -pix_fmt bgra -s {width}x{height} -r {fps} -i - " +
                              $"-f s16le -ar 48000 -ac 2 -i \"{pipePath}\" " +
                              $"-c:v {encoder} -b:v {videoBitrate}k -maxrate {videoBitrate}k -bufsize {videoBitrate * 2}k -preset veryfast -g {fps * 2} -pix_fmt yuv420p " +
                              $"-c:a aac -b:a {audioBitrate}k -f flv \"{rtmpTarget}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _ffmpegProcess = new Process { StartInfo = psi };
                _ffmpegProcess.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data?.Contains("bitrate=") == true)
                    {
                        CurrentStatus = StreamHealthStatus.Excellent;
                    }
                };

                _ffmpegProcess.Start();
                _ffmpegProcess.BeginErrorReadLine();

                _videoInputStream = _ffmpegProcess.StandardInput.BaseStream;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _audioPipeServer.WaitForConnectionAsync();
                        _audioInputStream = _audioPipeServer;
                    }
                    catch { }
                });

                lock (_stateLock)
                {
                    _isStreaming = true;
                    _stopwatch.Restart();
                    CurrentStatus = StreamHealthStatus.Connecting;
                }

                StartStatsMonitoring(profile.StreamBitrateKbps);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start stream: {ex.Message}");
                StopStreaming();
                return false;
            }
        }

        public void WriteVideoFrame(byte[] pixelBytes)
        {
            lock (_stateLock)
            {
                if (!_isStreaming || _videoInputStream == null) return;
                try
                {
                    _videoInputStream.Write(pixelBytes, 0, pixelBytes.Length);
                }
                catch { }
            }
        }

        public void WriteAudioSamples(byte[] audioBytes, int count)
        {
            lock (_stateLock)
            {
                if (!_isStreaming || _audioInputStream == null) return;
                try
                {
                    _audioInputStream.Write(audioBytes, 0, count);
                }
                catch { }
            }
        }

        public void StopStreaming()
        {
            lock (_stateLock)
            {
                if (!_isStreaming) return;
                _isStreaming = false;
                _stopwatch.Stop();
                CurrentStatus = StreamHealthStatus.Offline;
            }

            try
            {
                _videoInputStream?.Flush();
                _videoInputStream?.Close();
                _videoInputStream?.Dispose();
                _videoInputStream = null;

                _audioInputStream?.Flush();
                _audioInputStream?.Close();
                _audioInputStream?.Dispose();
                _audioInputStream = null;

                _audioPipeServer?.Dispose();
                _audioPipeServer = null;

                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    _ffmpegProcess.WaitForExit(2000);
                    if (!_ffmpegProcess.HasExited)
                    {
                        _ffmpegProcess.Kill();
                    }
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                }
            }
            catch { }
        }

        private void StartStatsMonitoring(int targetBitrate)
        {
            _ = Task.Run(async () =>
            {
                while (_isStreaming)
                {
                    await Task.Delay(1000);
                    if (!_isStreaming) break;

                    var stats = new StreamStats
                    {
                        Uptime = _stopwatch.Elapsed,
                        BitrateKbps = targetBitrate,
                        Status = CurrentStatus,
                        DroppedFrames = 0
                    };

                    StatsUpdated?.Invoke(stats);
                }
            });
        }

        public void Dispose()
        {
            StopStreaming();
        }
    }
}
