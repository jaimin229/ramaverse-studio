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
    public class RecordingStats
    {
        public TimeSpan ElapsedTime { get; set; }
        public long BytesWritten { get; set; }
        public double FileSizeMb => BytesWritten / (1024.0 * 1024.0);
        public double CurrentBitrateKbps { get; set; }
        public bool IsPaused { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Failure reason surfaced to the UI when a recording cannot start or dies
    /// mid-recording, so the user gets an actionable message instead of silence.
    /// </summary>
    public enum RecordingFailure
    {
        None,
        FFmpegNotFound,
        FFmpegCrashed,
        DiskFull,
        Unknown
    }

    public class FFmpegRecordingEngine : IDisposable
    {
        private Process? _ffmpegProcess;
        private Stream? _videoInputStream;
        private Stream? _audioInputStream;
        private NamedPipeServerStream? _audioPipeServer;
        private string _audioPipeName = "";

        // Multi-track recording: second isolated audio pipe + queue
        private Stream? _desktopAudioInputStream;
        private NamedPipeServerStream? _desktopAudioPipeServer;
        private string _desktopAudioPipeName = "";
        private BlockingCollection<byte[]>? _desktopAudioQueue;
        private Task? _desktopAudioPumpTask;

        private BlockingCollection<SharedFrame>? _videoQueue;
        private BlockingCollection<byte[]>? _audioQueue;
        private Task? _videoPumpTask;
        private Task? _audioPumpTask;

        private readonly object _stateLock = new object();
        private bool _isRecording = false;
        private bool _isPaused = false;
        private Stopwatch _stopwatch = new Stopwatch();
        private long _pauseOffsetMs = 0;
        private long _pauseStartMs = 0;
        private string _currentOutputFilePath = "";
        private string _currentMkvCapturePath = "";
        private StudioProfile? _activeProfile;
        private int _frameWidth;
        private int _frameHeight;
        private int _frameStride;
        private double _lastBitrateSampleMb = 0;

        // FFmpeg stderr ring buffer (last ~4KB) for crash diagnosis
        private readonly StringBuilder _errorLog = new StringBuilder(4096);
        private int _errorLogLength = 0;

        public bool IsRecording => _isRecording;
        public bool IsPaused => _isPaused;
        public TimeSpan ElapsedTime => TimeSpan.FromMilliseconds(Math.Max(0, _stopwatch.ElapsedMilliseconds - _pauseOffsetMs));
        public string CurrentOutputFilePath => _currentOutputFilePath;
        public string CurrentMkvCapturePath => _currentMkvCapturePath;
        public RecordingFailure LastFailure { get; private set; } = RecordingFailure.None;
        public string LastErrorDetails => _errorLog.ToString();

        public event Action<RecordingStats>? StatsUpdated;
        public event Action<RecordingFailure, string>? RecordingFailed;

        public static string ResolveEncoderString(VideoEncoder encoder) =>
            FFmpegArgsBuilder.ResolveEncoderString(encoder);

        public async Task<(bool Success, RecordingFailure Failure, string Details)> StartRecordingAsync(StudioProfile profile)
        {
            lock (_stateLock)
            {
                if (_isRecording) return (false, RecordingFailure.None, "Already recording.");
            }

            if (!FFmpegPathResolver.TryGetRealPath(out _))
            {
                return (false, RecordingFailure.FFmpegNotFound, FFmpegPathResolver.GetMissingFfmpegHelpMessage());
            }

            try
            {
                Directory.CreateDirectory(profile.RecordingDirectory);
                string ext = profile.RecFormat.ToString().ToLower();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _currentOutputFilePath = Path.Combine(profile.RecordingDirectory, $"Ramaverse_{timestamp}.{ext}");
                _currentMkvCapturePath = FFmpegArgsBuilder.UsesMkvSafetyCapture(profile)
                    ? Path.Combine(profile.RecordingDirectory, $"Ramaverse_{timestamp}.mkv")
                    : _currentOutputFilePath;

                int width = profile.CanvasWidth;
                int height = profile.CanvasHeight;
                int fps = profile.Fps;

                _frameWidth = width;
                _frameHeight = height;
                _frameStride = width * 4;

                _activeProfile = profile;
                LastFailure = RecordingFailure.None;
                _errorLog.Clear();
                _errorLogLength = 0;

                _audioPipeName = $"RamaverseAudio_{Guid.NewGuid():N}";
                _audioPipeServer = new NamedPipeServerStream(_audioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);

                string pipePath = $@"\\.\pipe\{_audioPipeName}";

                bool multiTrack = profile.MultiTrackAudioRecording;
                string args;
                if (multiTrack)
                {
                    _desktopAudioPipeName = $"RamaverseDeskAudio_{Guid.NewGuid():N}";
                    _desktopAudioPipeServer = new NamedPipeServerStream(_desktopAudioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);
                    string deskPipePath = $@"\\.\pipe\{_desktopAudioPipeName}";
                    args = FFmpegArgsBuilder.BuildMultiTrackRecordingArgs(profile, width, height, fps, pipePath, deskPipePath, _currentOutputFilePath);
                }
                else
                {
                    args = FFmpegArgsBuilder.BuildRecordingArgs(profile, width, height, fps, pipePath, _currentOutputFilePath);
                }
                var psi = new ProcessStartInfo
                {
                    FileName = FFmpegPathResolver.GetFFmpegPath(),
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                _ffmpegProcess = new Process { StartInfo = psi };
                _ffmpegProcess.ErrorDataReceived += OnFfmpegErrorLine;
                _ffmpegProcess.EnableRaisingEvents = true;
                _ffmpegProcess.Exited += OnFfmpegExited;

                _ffmpegProcess.Start();
                _ffmpegProcess.BeginErrorReadLine();

                _videoInputStream = _ffmpegProcess.StandardInput.BaseStream;

                // Create the queues BEFORE the pipe wait so early frames buffer
                // instead of being dropped.
                _videoQueue = new BlockingCollection<SharedFrame>(60);
                _audioQueue = new BlockingCollection<byte[]>(200);

                // Declare ourselves live FIRST: WriteVideoFrame is gated on
                // _isRecording, and FFmpeg cannot open the audio pipe until it
                // has received stdin video — which requires frames flowing.
                lock (_stateLock)
                {
                    _isRecording = true;
                    _isPaused = false;
                    _stopwatch.Restart();
                    _pauseOffsetMs = 0;
                }

                // Start the stdin video pump IMMEDIATELY. FFmpeg opens inputs in
                // argument order: it must read enough stdin video to probe the
                // rawvideo format before it ever opens the audio pipe. Feeding
                // stdin concurrently is what lets WaitForConnection succeed.
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
                        Debug.WriteLine($"Recording video pump died: {ex.Message}");
                        FramePumpFailed();
                    }
                });

                // Now FFmpeg can open the audio pipes (it has its video probing data).
                var connectTask = _audioPipeServer.WaitForConnectionAsync();
                var deskConnectTask = _desktopAudioPipeServer?.WaitForConnectionAsync() ?? Task.CompletedTask;

                if (await Task.WhenAny(connectTask, Task.Delay(15000)) != connectTask)
                {
                    StopRecording();
                    return (false, RecordingFailure.FFmpegCrashed, "FFmpeg did not open the audio pipe in time. " + GetRecentErrors());
                }

                if (deskConnectTask != Task.CompletedTask &&
                    await Task.WhenAny(deskConnectTask, Task.Delay(15000)) != deskConnectTask)
                {
                    StopRecording();
                    return (false, RecordingFailure.FFmpegCrashed, "FFmpeg did not open the desktop-audio pipe in time. " + GetRecentErrors());
                }

                _audioInputStream = _audioPipeServer;
                _desktopAudioInputStream = _desktopAudioPipeServer;
                _desktopAudioQueue = _desktopAudioPipeServer != null
                    ? new BlockingCollection<byte[]>(200)
                    : null;

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
                        Debug.WriteLine($"Recording audio pump died: {ex.Message}");
                        FramePumpFailed();
                    }
                });

                if (_desktopAudioQueue != null)
                {
                    var deskQueue = _desktopAudioQueue;
                    _desktopAudioPumpTask = Task.Run(() =>
                    {
                        try
                        {
                            foreach (var chunk in deskQueue.GetConsumingEnumerable())
                            {
                                _desktopAudioInputStream?.Write(chunk, 0, chunk.Length);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Desktop audio pump died: {ex.Message}");
                            FramePumpFailed();
                        }
                    });
                }

                StartStatsMonitoring();
                return (true, RecordingFailure.None, "");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start recording: {ex.Message}");
                StopRecording();
                return (false, RecordingFailure.Unknown, ex.Message);
            }
        }

        private void OnFfmpegErrorLine(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            lock (_errorLog)
            {
                if (_errorLogLength > 3000)
                {
                    _errorLog.Remove(0, 2048);
                }
                _errorLog.AppendLine(e.Data);
                _errorLogLength = _errorLog.Length;
            }
        }

        private string GetRecentErrors()
        {
            lock (_errorLog)
            {
                return _errorLog.ToString();
            }
        }

        /// <summary>
        /// Detects FFmpeg dying mid-recording (invalid encoder, disk full) and
        /// surfaces the reason to the UI immediately.
        /// </summary>
        private void OnFfmpegExited(object? sender, EventArgs e)
        {
            bool wasRecording;
            lock (_stateLock) { wasRecording = _isRecording; }

            if (wasRecording)
            {
                string details = GetRecentErrors();
                var failure = DetectFailure(details);
                LastFailure = failure;

                string friendly = failure switch
                {
                    RecordingFailure.FFmpegNotFound => FFmpegPathResolver.GetMissingFfmpegHelpMessage(),
                    RecordingFailure.FFmpegCrashed => $"FFmpeg stopped unexpectedly:\n{Truncate(details, 600)}",
                    RecordingFailure.DiskFull => "Your disk ran out of space while recording. The file has been kept up to the last moment.",
                    _ => $"Recording engine error:\n{Truncate(details, 600)}"
                };

                RecordingFailed?.Invoke(failure, friendly);

                lock (_stateLock)
                {
                    _isRecording = false;
                    _stopwatch.Stop();
                }
            }
        }

        private static RecordingFailure DetectFailure(string stderr)
        {
            if (stderr.Contains("No space left", StringComparison.OrdinalIgnoreCase) ||
                stderr.Contains("No space on device", StringComparison.OrdinalIgnoreCase))
            {
                return RecordingFailure.DiskFull;
            }
            return RecordingFailure.FFmpegCrashed;
        }

        private void FramePumpFailed()
        {
            // Kill FFmpeg: no point continuing with a broken pipe. Exited handler reports.
            try { _ffmpegProcess?.Kill(); } catch { }
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) ? "(no FFmpeg output)" : (s.Length <= max ? s : s[^max..]);

        public void PauseRecording()
        {
            lock (_stateLock)
            {
                if (!_isRecording || _isPaused) return;
                _isPaused = true;
                _pauseStartMs = _stopwatch.ElapsedMilliseconds;
            }
        }

        public void ResumeRecording()
        {
            lock (_stateLock)
            {
                if (!_isRecording || !_isPaused) return;
                _isPaused = false;
                _pauseOffsetMs += (_stopwatch.ElapsedMilliseconds - _pauseStartMs);
            }
        }

        /// <summary>
        /// Accepts a composited frame. The caller must already hold one reference
        /// for this call; ownership of that reference transfers to the queue.
        /// If the queue is full the frame is released immediately (drop, never block).
        /// </summary>
        public void WriteVideoFrame(SharedFrame frame)
        {
            BlockingCollection<SharedFrame>? queue;
            lock (_stateLock)
            {
                if (!_isRecording || _isPaused) { queue = null; }
                else { queue = _videoQueue; }
            }

            if (queue == null || queue.IsAddingCompleted || !queue.TryAdd(frame))
            {
                frame.Release();
            }
        }

        public void WriteAudioSamples(byte[] audioBytes, int count)
        {
            BlockingCollection<byte[]>? queue;
            lock (_stateLock)
            {
                queue = (_isRecording && !_isPaused) ? _audioQueue : null;
            }
            if (queue == null) return;

            try
            {
                byte[] chunk = new byte[count];
                Buffer.BlockCopy(audioBytes, 0, chunk, 0, count);
                if (!queue.TryAdd(chunk))
                {
                    // Bounded queue full: keep newest audio, drop oldest.
                    if (queue.TryTake(out _))
                    {
                        queue.TryAdd(chunk);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Isolated mic track for multi-track recording (input #1 = the primary
        /// audio pipe when MultiTrackAudioRecording is on).
        /// Silently no-ops when single-track mode is active.
        /// </summary>
        public void WriteMicTrackSamples(byte[] audioBytes, int count)
        {
            BlockingCollection<byte[]>? queue;
            lock (_stateLock)
            {
                queue = (_isRecording && !_isPaused) ? _audioQueue : null;
            }
            if (queue == null) return;

            EnqueueBounded(queue, audioBytes, count);
        }

        public void WriteDesktopTrackSamples(byte[] audioBytes, int count)
        {
            BlockingCollection<byte[]>? queue;
            lock (_stateLock)
            {
                queue = (_isRecording && !_isPaused) ? _desktopAudioQueue : null;
            }
            if (queue == null) return;

            EnqueueBounded(queue, audioBytes, count);
        }

        private static void EnqueueBounded(BlockingCollection<byte[]> queue, byte[] src, int count)
        {
            try
            {
                byte[] chunk = new byte[count];
                Buffer.BlockCopy(src, 0, chunk, 0, count);
                if (!queue.TryAdd(chunk))
                {
                    if (queue.TryTake(out _))
                    {
                        queue.TryAdd(chunk);
                    }
                }
            }
            catch { }
        }

        public void StopRecording()
        {
            lock (_stateLock)
            {
                if (!_isRecording)
                {
                    // Still tear down any half-started resources.
                    TearDownResources();
                    return;
                }
                _isRecording = false;
                _isPaused = false;
                _stopwatch.Stop();
            }

            try
            {
                _videoQueue?.CompleteAdding();
                _audioQueue?.CompleteAdding();
                _desktopAudioQueue?.CompleteAdding();

                Task.WaitAll(new[]
                {
                    _videoPumpTask ?? Task.CompletedTask,
                    _audioPumpTask ?? Task.CompletedTask,
                    _desktopAudioPumpTask ?? Task.CompletedTask
                }, 3000);

                // Closing stdin signals EOF; FFmpeg finalizes the MP4 index.
                try { _videoInputStream?.Flush(); } catch { }
                try { _videoInputStream?.Close(); } catch { }
                _videoInputStream = null;

                try { _audioInputStream?.Close(); } catch { }
                _audioInputStream = null;
                try { _audioPipeServer?.Dispose(); } catch { }
                _audioPipeServer = null;

                try { _desktopAudioInputStream?.Close(); } catch { }
                _desktopAudioInputStream = null;
                try { _desktopAudioPipeServer?.Dispose(); } catch { }
                _desktopAudioPipeServer = null;

                if (_ffmpegProcess != null)
                {
                    // Give FFmpeg up to 5 seconds to finalize the container.
                    if (!_ffmpegProcess.WaitForExit(5000))
                    {
                        try { _ffmpegProcess.Kill(); } catch { }
                        _ffmpegProcess.WaitForExit(1000);
                    }
                    _ffmpegProcess.Dispose();
                    _ffmpegProcess = null;
                }
            }
            catch { }

            // Crash-proof capture: remux the MKV into the user's chosen
            // container (stream-copy, no re-encode). If this fails the MKV
            // itself remains, fully playable.
            RemuxCaptureIfNeeded();

            TearDownResources();
        }

        /// <summary>
        /// Stream-copy remux of the crash-proof MKV capture into the user's
        /// chosen container. Runs synchronously (typically 1-3 seconds per
        // hour of footage) so the completed dialog reflects the real file.
        /// </summary>
        private void RemuxCaptureIfNeeded()
        {
            string mkv = _currentMkvCapturePath;
            string target = _currentOutputFilePath;

            if (string.IsNullOrEmpty(mkv) || string.IsNullOrEmpty(target) ||
                mkv == target || !File.Exists(mkv))
            {
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = FFmpegPathResolver.GetFFmpegPath(),
                    Arguments = FFmpegArgsBuilder.BuildRemuxArgs(mkv, target),
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc != null && !proc.WaitForExit(60000))
                {
                    try { proc.Kill(); } catch { }
                    return; // leave MKV intact on remux failure
                }

                // Only delete the intermediate MKV when the target exists and
                // has plausible content.
                if (proc != null && proc.ExitCode == 0 && File.Exists(target) && new FileInfo(target).Length > 1024)
                {
                    try { File.Delete(mkv); } catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Remux failed (MKV preserved): {ex.Message}");
            }
        }

        /// <summary>
        /// Called on app startup: finds MKV captures left behind by a previous
        /// crash (no matching final file) and remuxes them so the user never
        /// loses a session to a power cut or crash. Returns recovered file paths.
        /// </summary>
        public static List<string> RecoverOrphanedCaptures(string recordingDirectory)
        {
            var recovered = new List<string>();
            try
            {
                if (!Directory.Exists(recordingDirectory)) return recovered;

                foreach (var mkv in Directory.GetFiles(recordingDirectory, "Ramaverse_*.mkv"))
                {
                    string target = Path.ChangeExtension(mkv, ".mp4");
                    if (File.Exists(target)) continue; // already remuxed once
                    if (new FileInfo(mkv).Length < 8192) continue; // nothing usable

                    var psi = new ProcessStartInfo
                    {
                        FileName = FFmpegPathResolver.GetFFmpegPath(),
                        Arguments = FFmpegArgsBuilder.BuildRemuxArgs(mkv, target),
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };

                    using var proc = Process.Start(psi);
                    if (proc != null && proc.WaitForExit(60000) && proc.ExitCode == 0 &&
                        File.Exists(target) && new FileInfo(target).Length > 1024)
                    {
                        try { File.Delete(mkv); } catch { }
                        recovered.Add(target);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Orphan recovery failed: {ex.Message}");
            }
            return recovered;
        }

        private void TearDownResources()
        {
            _videoQueue = null;
            _audioQueue = null;
            _desktopAudioQueue = null;
            _videoPumpTask = null;
            _audioPumpTask = null;
            _desktopAudioPumpTask = null;
        }

        private void StartStatsMonitoring()
        {
            var process = _ffmpegProcess;
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    bool recording, paused;
                    lock (_stateLock)
                    {
                        recording = _isRecording;
                        paused = _isPaused;
                        if (!recording && process != null && process.HasExited) break;
                        if (!recording) break;
                    }

                    await Task.Delay(500);

                    long bytesWritten = 0;
                    try
                    {
                        string activeFile = (!string.IsNullOrEmpty(_currentMkvCapturePath) && File.Exists(_currentMkvCapturePath))
                            ? _currentMkvCapturePath
                            : _currentOutputFilePath;

                        if (!string.IsNullOrEmpty(activeFile) && File.Exists(activeFile))
                        {
                            bytesWritten = new FileInfo(activeFile).Length;
                        }
                    }
                    catch { }

                    double deltaMb = bytesWritten / (1024.0 * 1024.0) - _lastBitrateSampleMb;
                    double kbps = Math.Max(0, deltaMb * 1024 * 8 * 2); // 500ms sample
                    _lastBitrateSampleMb = bytesWritten / (1024.0 * 1024.0);

                    var stats = new RecordingStats
                    {
                        ElapsedTime = TimeSpan.FromMilliseconds(Math.Max(0, _stopwatch.ElapsedMilliseconds - _pauseOffsetMs)),
                        BytesWritten = bytesWritten,
                        CurrentBitrateKbps = kbps,
                        IsPaused = paused,
                        IsActive = recording
                    };

                    StatsUpdated?.Invoke(stats);
                }
            });
        }

        public void OpenOutputFile()
        {
            try
            {
                if (File.Exists(_currentOutputFilePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _currentOutputFilePath,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        public void OpenOutputFolder()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_currentOutputFilePath))
                {
                    string dir = Path.GetDirectoryName(_currentOutputFilePath) ?? "";
                    if (Directory.Exists(dir))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"/select,\"{_currentOutputFilePath}\"",
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            StopRecording();
        }
    }
}
