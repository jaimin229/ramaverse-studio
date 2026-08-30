using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using RamaverseStudio.Models;

namespace RamaverseStudio.Output
{
    public enum StreamHealthStatus
    {
        Offline,
        Connecting,
        Good,
        Warning,
        Critical
    }

    public class StreamStats
    {
        public TimeSpan Uptime { get; set; }
        public double CurrentKbps { get; set; }
        public double BitrateKbps { get => CurrentKbps; set => CurrentKbps = value; }
        public double TargetKbps { get; set; }
        public int Fps { get; set; }
        public long DroppedFrames { get; set; }
        public double DroppedPercentage { get; set; }
        public StreamHealthStatus Health { get; set; }
        public StreamHealthStatus Status { get => Health; set => Health = value; }
    }

    public class FFmpegStreamingEngine : IDisposable
    {
        private Process? _ffmpegProcess;
        private Stream? _videoInputStream;
        private Stream? _audioInputStream;
        private NamedPipeServerStream? _audioPipeServer;
        private string _audioPipeName = "";

        private BlockingCollection<byte[]>? _videoQueue;
        private BlockingCollection<byte[]>? _audioQueue;
        private Task? _videoPumpTask;
        private Task? _audioPumpTask;

        private readonly object _stateLock = new object();
        private bool _isStreaming = false;
        private Stopwatch _stopwatch = new Stopwatch();

        private long _totalFramesPushed = 0;
        private long _totalFramesDropped = 0;

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
                if (string.IsNullOrWhiteSpace(profile.StreamKey))
                {
                    throw new InvalidOperationException("Stream Key is required to start streaming.");
                }

                string serverUrl = profile.RtmpServerUrl.TrimEnd('/');
                string targetUrl = $"{serverUrl}/{profile.StreamKey}";

                int width = profile.CanvasWidth;
                int height = profile.CanvasHeight;
                int fps = profile.Fps;
                int videoBitrate = profile.StreamBitrateKbps;
                int audioBitrate = profile.AudioBitrateKbps;
                string encoder = FFmpegRecordingEngine.ResolveEncoderString(profile.Encoder);

                _audioPipeName = $"RamaverseStreamAudio_{Guid.NewGuid():N}";
                _audioPipeServer = new NamedPipeServerStream(_audioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);

                string pipePath = $@"\\.\pipe\{_audioPipeName}";

                string args = $"-y -f rawvideo -pix_fmt bgra -s {width}x{height} -r {fps} -i - " +
                              $"-f s16le -ar 48000 -ac 2 -i \"{pipePath}\" " +
                              $"-c:v {encoder} -b:v {videoBitrate}k -maxrate {videoBitrate}k -bufsize {videoBitrate * 2}k -preset veryfast -g {fps * 2} -pix_fmt yuv420p " +
                              $"-c:a aac -b:a {audioBitrate}k -ar 48000 " +
                              $"-f flv \"{targetUrl}\"";

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
                _ffmpegProcess.ErrorDataReceived += (s, e) => { };

                _ffmpegProcess.Start();
                _ffmpegProcess.BeginErrorReadLine();

                _videoInputStream = _ffmpegProcess.StandardInput.BaseStream;

                var connectTask = _audioPipeServer.WaitForConnectionAsync();
                if (await Task.WhenAny(connectTask, Task.Delay(3000)) == connectTask)
                {
                    _audioInputStream = _audioPipeServer;
                }

                _videoQueue = new BlockingCollection<byte[]>(30);
                _audioQueue = new BlockingCollection<byte[]>(100);

                _videoPumpTask = Task.Run(() =>
                {
                    try
                    {
                        foreach (var frame in _videoQueue.GetConsumingEnumerable())
                        {
                            _videoInputStream?.Write(frame, 0, frame.Length);
                        }
                        _videoInputStream?.Flush();
                    }
                    catch { }
                });

                _audioPumpTask = Task.Run(() =>
                {
                    try
                    {
                        foreach (var chunk in _audioQueue.GetConsumingEnumerable())
                        {
                            _audioInputStream?.Write(chunk, 0, chunk.Length);
                        }
                        _audioInputStream?.Flush();
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
                if (!_isStreaming || _videoQueue == null || _videoQueue.IsAddingCompleted) return;
                _totalFramesPushed++;
            }

            try
            {
                if (!_videoQueue.TryAdd(pixelBytes))
                {
                    Interlocked.Increment(ref _totalFramesDropped);
                }
            }
            catch { }
        }

        public void WriteAudioSamples(byte[] audioBytes, int count)
        {
            lock (_stateLock)
            {
                if (!_isStreaming || _audioQueue == null || _audioQueue.IsAddingCompleted) return;
            }

            try
            {
                byte[] chunk = new byte[count];
                Buffer.BlockCopy(audioBytes, 0, chunk, 0, count);
                _audioQueue.TryAdd(chunk);
            }
            catch { }
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
                _videoQueue?.CompleteAdding();
                _audioQueue?.CompleteAdding();

                Task.WaitAll(new[] { _videoPumpTask ?? Task.CompletedTask, _audioPumpTask ?? Task.CompletedTask }, 2000);

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
                    _ffmpegProcess.WaitForExit(3000);
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

        private void StartStatsMonitoring(int targetKbps)
        {
            _ = Task.Run(async () =>
            {
                while (_isStreaming)
                {
                    await Task.Delay(1000);
                    if (!_isStreaming) break;

                    long dropped = Interlocked.Read(ref _totalFramesDropped);
                    long total = Interlocked.Read(ref _totalFramesPushed);
                    double dropRate = total > 0 ? (double)dropped / total * 100.0 : 0;

                    var status = StreamHealthStatus.Good;
                    if (dropRate > 10.0) status = StreamHealthStatus.Critical;
                    else if (dropRate > 2.0) status = StreamHealthStatus.Warning;

                    CurrentStatus = status;

                    var stats = new StreamStats
                    {
                        Uptime = _stopwatch.Elapsed,
                        TargetKbps = targetKbps,
                        CurrentKbps = targetKbps * (1.0 - (dropRate / 100.0)),
                        DroppedFrames = dropped,
                        DroppedPercentage = dropRate,
                        Health = status
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
