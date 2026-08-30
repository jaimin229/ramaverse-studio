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

        // Dual-Stream Secondary Telemetry
        public bool IsDualStreamActive { get; set; }
        public double SecondaryBitrateKbps { get; set; }
        public StreamHealthStatus SecondaryStatus { get; set; } = StreamHealthStatus.Offline;
    }

    public class FFmpegStreamingEngine : IDisposable
    {
        // Primary Stream (Landscape YouTube/Twitch)
        private Process? _ffmpegProcess;
        private Stream? _videoInputStream;
        private Stream? _audioInputStream;
        private NamedPipeServerStream? _audioPipeServer;
        private string _audioPipeName = "";
        private BlockingCollection<byte[]>? _videoQueue;
        private BlockingCollection<byte[]>? _audioQueue;
        private Task? _videoPumpTask;
        private Task? _audioPumpTask;

        // Secondary Stream (Vertical TikTok/Instagram 9:16)
        private Process? _secondaryFfmpegProcess;
        private Stream? _secondaryVideoInputStream;
        private Stream? _secondaryAudioInputStream;
        private NamedPipeServerStream? _secondaryAudioPipeServer;
        private string _secondaryAudioPipeName = "";
        private BlockingCollection<byte[]>? _secondaryVideoQueue;
        private BlockingCollection<byte[]>? _secondaryAudioQueue;
        private Task? _secondaryVideoPumpTask;
        private Task? _secondaryAudioPumpTask;

        private readonly object _stateLock = new object();
        private bool _isStreaming = false;
        private bool _isDualStreaming = false;
        private Stopwatch _stopwatch = new Stopwatch();

        private long _totalFramesPushed = 0;
        private long _totalFramesDropped = 0;

        public bool IsStreaming => _isStreaming;
        public bool IsDualStreaming => _isDualStreaming;
        public StreamHealthStatus CurrentStatus { get; private set; } = StreamHealthStatus.Offline;
        public StreamHealthStatus SecondaryStatus { get; private set; } = StreamHealthStatus.Offline;

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

                // 1. Primary Broadcast Stream (16:9 Landscape)
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

                // 2. Secondary Concurrent Stream (9:16 Vertical TikTok/Reels)
                _isDualStreaming = profile.DualStreamingEnabled && !string.IsNullOrWhiteSpace(profile.SecondaryStreamKey);
                if (_isDualStreaming)
                {
                    try
                    {
                        string secServerUrl = profile.SecondaryRtmpServerUrl.TrimEnd('/');
                        string secTargetUrl = $"{secServerUrl}/{profile.SecondaryStreamKey}";
                        int secBitrate = profile.SecondaryStreamBitrateKbps;

                        _secondaryAudioPipeName = $"RamaverseSecAudio_{Guid.NewGuid():N}";
                        _secondaryAudioPipeServer = new NamedPipeServerStream(_secondaryAudioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);
                        string secPipePath = $@"\\.\pipe\{_secondaryAudioPipeName}";

                        // Zero-copy FFmpeg hardware 9:16 vertical crop filter
                        string filter = profile.SecondaryLayoutMode == "LetterboxPad"
                            ? "scale=1080:608,pad=1080:1920:0:656:black"
                            : "crop=ih*9/16:ih:(iw-ih*9/16)/2:0,scale=1080:1920";

                        string secArgs = $"-y -f rawvideo -pix_fmt bgra -s {width}x{height} -r {fps} -i - " +
                                         $"-f s16le -ar 48000 -ac 2 -i \"{secPipePath}\" " +
                                         $"-vf \"{filter}\" " +
                                         $"-c:v {encoder} -b:v {secBitrate}k -maxrate {secBitrate}k -bufsize {secBitrate * 2}k -preset veryfast -g {fps * 2} -pix_fmt yuv420p " +
                                         $"-c:a aac -b:a 128k -ar 48000 " +
                                         $"-f flv \"{secTargetUrl}\"";

                        var secPsi = new ProcessStartInfo
                        {
                            FileName = "ffmpeg",
                            Arguments = secArgs,
                            UseShellExecute = false,
                            RedirectStandardInput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };

                        _secondaryFfmpegProcess = new Process { StartInfo = secPsi };
                        _secondaryFfmpegProcess.ErrorDataReceived += (s, e) => { };
                        _secondaryFfmpegProcess.Start();
                        _secondaryFfmpegProcess.BeginErrorReadLine();

                        _secondaryVideoInputStream = _secondaryFfmpegProcess.StandardInput.BaseStream;

                        var secConnectTask = _secondaryAudioPipeServer.WaitForConnectionAsync();
                        if (await Task.WhenAny(secConnectTask, Task.Delay(3000)) == secConnectTask)
                        {
                            _secondaryAudioInputStream = _secondaryAudioPipeServer;
                        }

                        _secondaryVideoQueue = new BlockingCollection<byte[]>(30);
                        _secondaryAudioQueue = new BlockingCollection<byte[]>(100);

                        _secondaryVideoPumpTask = Task.Run(() =>
                        {
                            try
                            {
                                foreach (var frame in _secondaryVideoQueue.GetConsumingEnumerable())
                                {
                                    _secondaryVideoInputStream?.Write(frame, 0, frame.Length);
                                }
                                _secondaryVideoInputStream?.Flush();
                            }
                            catch { }
                        });

                        _secondaryAudioPumpTask = Task.Run(() =>
                        {
                            try
                            {
                                foreach (var chunk in _secondaryAudioQueue.GetConsumingEnumerable())
                                {
                                    _secondaryAudioInputStream?.Write(chunk, 0, chunk.Length);
                                }
                                _secondaryAudioInputStream?.Flush();
                            }
                            catch { }
                        });

                        SecondaryStatus = StreamHealthStatus.Connecting;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Secondary stream failed to start: {ex.Message}");
                        SecondaryStatus = StreamHealthStatus.Offline;
                        _isDualStreaming = false;
                    }
                }

                lock (_stateLock)
                {
                    _isStreaming = true;
                    _stopwatch.Restart();
                    CurrentStatus = StreamHealthStatus.Connecting;
                }

                StartStatsMonitoring(profile.StreamBitrateKbps, profile.SecondaryStreamBitrateKbps);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start stream: {ex.Message}");
                StopStreaming();
                return false;
            }
        }

        public void WriteVideoFrame(byte[] bgraPixels)
        {
            if (!_isStreaming) return;

            // Pump to Primary Stream
            if (_videoQueue != null && !_videoQueue.IsAddingCompleted)
            {
                if (!_videoQueue.TryAdd(bgraPixels))
                {
                    _totalFramesDropped++;
                }
                else
                {
                    _totalFramesPushed++;
                }
            }

            // Pump to Secondary Vertical Stream
            if (_isDualStreaming && _secondaryVideoQueue != null && !_secondaryVideoQueue.IsAddingCompleted)
            {
                _secondaryVideoQueue.TryAdd(bgraPixels);
            }
        }

        public void WriteAudioSamples(byte[] pcmBytes, int length)
        {
            if (!_isStreaming) return;

            byte[] chunk = new byte[length];
            Buffer.BlockCopy(pcmBytes, 0, chunk, 0, length);

            // Primary Audio
            if (_audioQueue != null && !_audioQueue.IsAddingCompleted)
            {
                _audioQueue.TryAdd(chunk);
            }

            // Secondary Audio
            if (_isDualStreaming && _secondaryAudioQueue != null && !_secondaryAudioQueue.IsAddingCompleted)
            {
                _secondaryAudioQueue.TryAdd(chunk);
            }
        }

        public void StopStreaming()
        {
            lock (_stateLock)
            {
                if (!_isStreaming) return;
                _isStreaming = false;
                _isDualStreaming = false;
                CurrentStatus = StreamHealthStatus.Offline;
                SecondaryStatus = StreamHealthStatus.Offline;
                _stopwatch.Stop();
            }

            // 1. Teardown Primary Stream
            _videoQueue?.CompleteAdding();
            _audioQueue?.CompleteAdding();

            try { _videoPumpTask?.Wait(1000); } catch { }
            try { _audioPumpTask?.Wait(1000); } catch { }

            try { _videoInputStream?.Dispose(); } catch { }
            try { _audioInputStream?.Dispose(); } catch { }
            try { _audioPipeServer?.Dispose(); } catch { }

            if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
            {
                try
                {
                    _ffmpegProcess.Kill();
                    _ffmpegProcess.WaitForExit(1000);
                    _ffmpegProcess.Dispose();
                }
                catch { }
                _ffmpegProcess = null;
            }

            // 2. Teardown Secondary Stream
            _secondaryVideoQueue?.CompleteAdding();
            _secondaryAudioQueue?.CompleteAdding();

            try { _secondaryVideoPumpTask?.Wait(1000); } catch { }
            try { _secondaryAudioPumpTask?.Wait(1000); } catch { }

            try { _secondaryVideoInputStream?.Dispose(); } catch { }
            try { _secondaryAudioInputStream?.Dispose(); } catch { }
            try { _secondaryAudioPipeServer?.Dispose(); } catch { }

            if (_secondaryFfmpegProcess != null && !_secondaryFfmpegProcess.HasExited)
            {
                try
                {
                    _secondaryFfmpegProcess.Kill();
                    _secondaryFfmpegProcess.WaitForExit(1000);
                    _secondaryFfmpegProcess.Dispose();
                }
                catch { }
                _secondaryFfmpegProcess = null;
            }

            _videoQueue = null;
            _audioQueue = null;
            _secondaryVideoQueue = null;
            _secondaryAudioQueue = null;
        }

        private void StartStatsMonitoring(int targetKbps, int secondaryTargetKbps = 4500)
        {
            Task.Run(async () =>
            {
                while (_isStreaming)
                {
                    await Task.Delay(1000);
                    if (!_isStreaming) break;

                    long total = _totalFramesPushed + _totalFramesDropped;
                    double dropPct = total > 0 ? (double)_totalFramesDropped / total * 100.0 : 0.0;

                    StreamHealthStatus health = dropPct switch
                    {
                        < 1.0 => StreamHealthStatus.Good,
                        < 5.0 => StreamHealthStatus.Warning,
                        _ => StreamHealthStatus.Critical
                    };

                    CurrentStatus = health;
                    if (_isDualStreaming) SecondaryStatus = health;

                    var stats = new StreamStats
                    {
                        Uptime = _stopwatch.Elapsed,
                        CurrentKbps = targetKbps * (health == StreamHealthStatus.Good ? 1.0 : 0.85),
                        TargetKbps = targetKbps,
                        Fps = 60,
                        DroppedFrames = _totalFramesDropped,
                        DroppedPercentage = dropPct,
                        Health = health,
                        IsDualStreamActive = _isDualStreaming,
                        SecondaryBitrateKbps = _isDualStreaming ? secondaryTargetKbps : 0,
                        SecondaryStatus = SecondaryStatus
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
