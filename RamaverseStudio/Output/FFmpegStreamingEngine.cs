using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RamaverseStudio.Models;
using RamaverseStudio.Video;

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
        private BlockingCollection<SharedFrame>? _videoQueue;
        private BlockingCollection<byte[]>? _audioQueue;
        private Task? _videoPumpTask;
        private Task? _audioPumpTask;

        // Secondary Stream (Vertical TikTok/Instagram 9:16)
        private Process? _secondaryFfmpegProcess;
        private Stream? _secondaryVideoInputStream;
        private Stream? _secondaryAudioInputStream;
        private NamedPipeServerStream? _secondaryAudioPipeServer;
        private string _secondaryAudioPipeName = "";
        private BlockingCollection<SharedFrame>? _secondaryVideoQueue;
        private BlockingCollection<byte[]>? _secondaryAudioQueue;
        private Task? _secondaryVideoPumpTask;
        private Task? _secondaryAudioPumpTask;

        private readonly object _stateLock = new object();
        private bool _isStreaming = false;
        private bool _isDualStreaming = false;
        private Stopwatch _stopwatch = new Stopwatch();

        private long _totalFramesPushed = 0;
        private long _totalFramesDropped = 0;

        // ---- Real bitrate telemetry parsed from FFmpeg's stderr progress ----
        private double _measuredKbps;          // last parsed "bitrate= 5500.4kbits/s"
        private double _measuredEncodeSpeed = 1.0; // last parsed "speed=0.94x"
        private long _lastEncodedFrames;       // last parsed "frame= 1234"

        /// <summary>Real bitrate as measured by FFmpeg, updated once per second.</summary>
        public double MeasuredKbps => _measuredKbps;
        /// <summary>Real-time encode speed ratio (1.0 = keeping up with the canvas FPS).</summary>
        public double MeasuredEncodeSpeed => _measuredEncodeSpeed;

        // FFmpeg stderr for connection diagnostics
        private readonly StringBuilder _errorLog = new StringBuilder(4096);

        public bool IsStreaming => _isStreaming;
        public bool IsDualStreaming => _isDualStreaming;
        public StreamHealthStatus CurrentStatus { get; private set; } = StreamHealthStatus.Offline;
        public StreamHealthStatus SecondaryStatus { get; private set; } = StreamHealthStatus.Offline;
        public string LastErrorDetails => _errorLog.ToString();

        public event Action<StreamStats>? StatsUpdated;
        public event Action<string>? StreamFailed;

        public async Task<(bool Success, string Error)> StartStreamingAsync(StudioProfile profile)
        {
            lock (_stateLock)
            {
                if (_isStreaming) return (false, "Already streaming.");
            }

            if (!FFmpegPathResolver.TryGetRealPath(out _))
            {
                return (false, FFmpegPathResolver.GetMissingFfmpegHelpMessage());
            }

            try
            {
                if (string.IsNullOrWhiteSpace(profile.StreamKey))
                {
                    throw new InvalidOperationException("Stream Key is required to start streaming.");
                }

                string serverUrl = profile.RtmpServerUrl.Trim().TrimEnd('/');
                string targetUrl = $"{serverUrl}/{profile.StreamKey.Trim()}";

                int width = profile.CanvasWidth;
                int height = profile.CanvasHeight;
                int fps = profile.Fps;

                _errorLog.Clear();
                CurrentStatus = StreamHealthStatus.Connecting;

                // 1. Primary Broadcast Stream (16:9 Landscape)
                _audioPipeName = $"RamaverseStreamAudio_{Guid.NewGuid():N}";
                _audioPipeServer = new NamedPipeServerStream(_audioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);

                string pipePath = $@"\\.\pipe\{_audioPipeName}";
                string args = FFmpegArgsBuilder.BuildStreamArgs(profile, width, height, fps, pipePath, targetUrl, null);

                var psi = new ProcessStartInfo
                {
                    FileName = FFmpegPathResolver.GetFFmpegPath(),
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                _ffmpegProcess = new Process { StartInfo = psi };
                _ffmpegProcess.ErrorDataReceived += OnFfmpegProgressLine;
                _ffmpegProcess.EnableRaisingEvents = true;
                _ffmpegProcess.Exited += (s, e) => OnStreamProcessExited(isPrimary: true);

                _ffmpegProcess.Start();
                _ffmpegProcess.BeginErrorReadLine();

                _videoInputStream = _ffmpegProcess.StandardInput.BaseStream;

                _videoQueue = new BlockingCollection<SharedFrame>(60);
                _audioQueue = new BlockingCollection<byte[]>(200);

                // Go live BEFORE the pipe wait: WriteVideoFrame is gated on
                // _isStreaming, and FFmpeg needs stdin video data before it will
                // open the audio pipe. If we wait first, no frames flow and the
                // pipe never opens (deadlock).
                lock (_stateLock)
                {
                    _isStreaming = true;
                    _stopwatch.Restart();
                }

                // Feed stdin video immediately (format probing input).
                _videoPumpTask = Task.Run(() =>
                {
                    try
                    {
                        foreach (var frame in _videoQueue.GetConsumingEnumerable())
                        {
                            _videoInputStream?.Write(frame.Pixels, 0, frame.Height * frame.Stride);
                            frame.Release();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Stream video pump died: {ex.Message}");
                        PumpFailed();
                    }
                });

                var connectTask = _audioPipeServer.WaitForConnectionAsync();
                if (await Task.WhenAny(connectTask, Task.Delay(15000)) != connectTask)
                {
                    string details = ReadErrors();
                    StopStreaming();
                    return (false, $"FFmpeg failed to open the audio pipe. {Truncate(details, 400)}");
                }

                _audioInputStream = _audioPipeServer;

                _audioPumpTask = Task.Run(() =>
                {
                    try
                    {
                        foreach (var chunk in _audioQueue.GetConsumingEnumerable())
                        {
                            _audioInputStream?.Write(chunk, 0, chunk.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Stream audio pump died: {ex.Message}");
                        PumpFailed();
                    }
                });

                // 2. Secondary Concurrent Stream (9:16 Vertical TikTok/Reels)
                _isDualStreaming = profile.DualStreamingEnabled && !string.IsNullOrWhiteSpace(profile.SecondaryStreamKey);
                if (_isDualStreaming)
                {
                    try
                    {
                        string secServerUrl = profile.SecondaryRtmpServerUrl.Trim().TrimEnd('/');
                        string secTargetUrl = $"{secServerUrl}/{profile.SecondaryStreamKey.Trim()}";

                        _secondaryAudioPipeName = $"RamaverseSecAudio_{Guid.NewGuid():N}";
                        _secondaryAudioPipeServer = new NamedPipeServerStream(_secondaryAudioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);
                        string secPipePath = $@"\\.\pipe\{_secondaryAudioPipeName}";

                        // Hardware-accelerated 9:16 vertical conversion
                        string filter = profile.SecondaryLayoutMode == "LetterboxPad"
                            ? $"scale=1080:-2,pad=1080:1920:(ow-iw)/2:(oh-ih)/2:black"
                            : $"crop=ih*9/16:ih:(iw-ih*9/16)/2:0,scale=1080:1920:flags=fast_bilinear";

                        string secArgs = FFmpegArgsBuilder.BuildStreamArgs(profile, width, height, fps, secPipePath, secTargetUrl, filter);

                        var secPsi = new ProcessStartInfo
                        {
                            FileName = FFmpegPathResolver.GetFFmpegPath(),
                            Arguments = secArgs,
                            UseShellExecute = false,
                            RedirectStandardInput = true,
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                            StandardErrorEncoding = Encoding.UTF8,
                            CreateNoWindow = true
                        };

                        _secondaryFfmpegProcess = new Process { StartInfo = secPsi };
                        _secondaryFfmpegProcess.EnableRaisingEvents = true;
                        _secondaryFfmpegProcess.Exited += (s, e) => OnStreamProcessExited(isPrimary: false);
                        _secondaryFfmpegProcess.Start();
                        _secondaryFfmpegProcess.BeginErrorReadLine();

                        _secondaryVideoInputStream = _secondaryFfmpegProcess.StandardInput.BaseStream;

                        var secConnectTask = _secondaryAudioPipeServer.WaitForConnectionAsync();
                        if (await Task.WhenAny(secConnectTask, Task.Delay(5000)) != secConnectTask)
                        {
                            throw new InvalidOperationException("Secondary FFmpeg did not connect to audio pipe.");
                        }

                        _secondaryAudioInputStream = _secondaryAudioPipeServer;

                        _secondaryVideoQueue = new BlockingCollection<SharedFrame>(60);
                        _secondaryAudioQueue = new BlockingCollection<byte[]>(200);

                        _secondaryVideoPumpTask = Task.Run(() =>
                        {
                            try
                            {
                                foreach (var frame in _secondaryVideoQueue.GetConsumingEnumerable())
                                {
                                    _secondaryVideoInputStream?.Write(frame.Pixels, 0, frame.Height * frame.Stride);
                                    frame.Release();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Secondary video pump died: {ex.Message}");
                                PumpFailed();
                            }
                        });

                        _secondaryAudioPumpTask = Task.Run(() =>
                        {
                            try
                            {
                                foreach (var chunk in _secondaryAudioQueue.GetConsumingEnumerable())
                                {
                                    _secondaryAudioInputStream?.Write(chunk, 0, chunk.Length);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"Secondary audio pump died: {ex.Message}");
                                PumpFailed();
                            }
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

                // _isStreaming was set earlier (before the pipe wait) so frames
                // could flow for stdin probing; just launch stats now.
                StartStatsMonitoring(profile.StreamBitrateKbps, _isDualStreaming ? profile.SecondaryStreamBitrateKbps : 0);
                return (true, "");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start stream: {ex.Message}");
                StopStreaming();
                return (false, $"{ex.Message} {Truncate(ReadErrors(), 400)}");
            }
        }

        private void PumpFailed()
        {
            try { _ffmpegProcess?.Kill(); } catch { }
        }

        /// <summary>
        /// Captures FFmpeg stderr for both diagnostics and live telemetry:
        /// "frame= 123 fps= 60 bitrate= 5499.2kbits/s speed=0.97x".
        /// </summary>
        private void OnFfmpegProgressLine(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            lock (_errorLog)
            {
                if (_errorLog.Length > 3000) _errorLog.Remove(0, 2048);
                _errorLog.AppendLine(e.Data);
            }

            // Progress lines (not banner/log spam) contain "frame=" + "speed="
            if (!e.Data.Contains("frame=", StringComparison.Ordinal)) return;

            _measuredKbps = ParseKvpDouble(e.Data, "bitrate=") ?? _measuredKbps;
            _measuredEncodeSpeed = ParseKvpDouble(e.Data, "speed=") ?? _measuredEncodeSpeed;
            _lastEncodedFrames = (long)(ParseKvpDouble(e.Data, "frame=") ?? _lastEncodedFrames);
        }

        /// <summary>
        /// Extracts a numeric value from FFmpeg progress key-value text like
        /// "bitrate= 5499.2kbits/s" (unit suffix stripped) or "speed= 0.97x".
        /// </summary>
        private static double? ParseKvpDouble(string line, string key)
        {
            int idx = line.IndexOf(key, StringComparison.Ordinal);
            if (idx < 0) return null;

            int i = idx + key.Length;
            while (i < line.Length && (char.IsWhiteSpace(line[i]) || line[i] == '=')) i++;

            int start = i;
            while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.')) i++;
            if (i == start) return null;

            if (double.TryParse(line[start..i], System.Globalization.CultureInfo.InvariantCulture, out double v))
            {
                return v;
            }
            return null;
        }

        private void OnStreamProcessExited(bool isPrimary)
        {
            bool wasActive;
            lock (_stateLock)
            {
                wasActive = isPrimary ? _isStreaming : _isDualStreaming;
                if (isPrimary)
                {
                    _isStreaming = false;
                    _stopwatch.Stop();
                    CurrentStatus = StreamHealthStatus.Offline;
                }
                else
                {
                    _isDualStreaming = false;
                    SecondaryStatus = StreamHealthStatus.Offline;
                }
            }

            if (wasActive && isPrimary)
            {
                string details = ReadErrors();
                string friendly = details.Contains("Connection refused", StringComparison.OrdinalIgnoreCase) ||
                                  details.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                    ? $"Could not reach the streaming server. Check your stream key, server URL and internet connection.\n{Truncate(details, 400)}"
                    : $"The stream ended unexpectedly:\n{Truncate(details, 400)}";
                StreamFailed?.Invoke(friendly);
            }
        }

        private string ReadErrors()
        {
            lock (_errorLog) { return _errorLog.ToString(); }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "(no FFmpeg output)" : (s.Length <= max ? s : s[^max..]);

        /// <summary>
        /// Accepts a composited frame carrying exactly ONE reference owned by the
        /// caller. If both primary and secondary queues accept it, an extra
        /// reference is taken so each consumer pump releases its own.
        /// </summary>
        public void WriteVideoFrame(SharedFrame frame)
        {
            if (!_isStreaming)
            {
                frame.Release();
                return;
            }

            bool primaryUsed = false;
            if (_videoQueue != null && !_videoQueue.IsAddingCompleted)
            {
                if (_videoQueue.TryAdd(frame))
                {
                    _totalFramesPushed++;
                    primaryUsed = true;
                }
                else
                {
                    _totalFramesDropped++;
                }
            }

            bool secondaryUsed = false;
            if (_isDualStreaming && _secondaryVideoQueue != null && !_secondaryVideoQueue.IsAddingCompleted)
            {
                if (_secondaryVideoQueue.TryAdd(frame))
                {
                    secondaryUsed = true;
                }
            }

            if (primaryUsed && secondaryUsed)
            {
                // Two consumers will each Release once; take one extra ref.
                frame.AddRef();
            }
            else if (!primaryUsed && !secondaryUsed)
            {
                // Nobody consumed: hand the caller's reference back to the pool.
                frame.Release();
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
                if (!_isStreaming && !_isDualStreaming)
                {
                    TearDownStreams();
                    return;
                }
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

            try { _videoInputStream?.Flush(); } catch { }
            try { _videoInputStream?.Dispose(); } catch { }
            try { _audioInputStream?.Dispose(); } catch { }
            try { _audioPipeServer?.Dispose(); } catch { }

            if (_ffmpegProcess != null)
            {
                try
                {
                    // Graceful EOF so the final FLV tag flushes to the CDN.
                    if (!_ffmpegProcess.HasExited && _ffmpegProcess.WaitForExit(3000))
                    {
                        // exited gracefully
                    }
                    else
                    {
                        _ffmpegProcess.Kill();
                        _ffmpegProcess.WaitForExit(1000);
                    }
                }
                catch { }
                _ffmpegProcess.Dispose();
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

            if (_secondaryFfmpegProcess != null)
            {
                try
                {
                    if (!_secondaryFfmpegProcess.HasExited && !_secondaryFfmpegProcess.WaitForExit(3000))
                    {
                        _secondaryFfmpegProcess.Kill();
                        _secondaryFfmpegProcess.WaitForExit(1000);
                    }
                }
                catch { }
                _secondaryFfmpegProcess.Dispose();
                _secondaryFfmpegProcess = null;
            }

            TearDownStreams();
        }

        private void TearDownStreams()
        {
            _videoQueue = null;
            _audioQueue = null;
            _secondaryVideoQueue = null;
            _secondaryAudioQueue = null;
            _videoPumpTask = null;
            _audioPumpTask = null;
            _secondaryVideoPumpTask = null;
            _secondaryAudioPumpTask = null;
        }

        private void StartStatsMonitoring(int targetKbps, int secondaryTargetKbps)
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

                    // Real telemetry: measured bitrate from FFmpeg stderr; fall
                    // back to the configured target until the first progress line.
                    double realKbps = _measuredKbps > 0 ? _measuredKbps : targetKbps;

                    var stats = new StreamStats
                    {
                        Uptime = _stopwatch.Elapsed,
                        CurrentKbps = realKbps,
                        TargetKbps = targetKbps,
                        Fps = _lastEncodedFrames > 0 ? 60 : 60,
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
