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
    public class RecordingStats
    {
        public TimeSpan ElapsedTime { get; set; }
        public long BytesWritten { get; set; }
        public double FileSizeMb => BytesWritten / (1024.0 * 1024.0);
        public double CurrentBitrateKbps { get; set; }
        public bool IsPaused { get; set; }
    }

    public class FFmpegRecordingEngine : IDisposable
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
        private bool _isRecording = false;
        private bool _isPaused = false;
        private Stopwatch _stopwatch = new Stopwatch();
        private long _pauseOffsetMs = 0;
        private long _pauseStartMs = 0;
        private string _currentOutputFilePath = "";

        public bool IsRecording => _isRecording;
        public bool IsPaused => _isPaused;
        public string CurrentOutputFilePath => _currentOutputFilePath;

        public event Action<RecordingStats>? StatsUpdated;

        public static string ResolveEncoderString(VideoEncoder encoder)
        {
            return encoder switch
            {
                VideoEncoder.NvidiaNvencH264 => "h264_nvenc",
                VideoEncoder.NvidiaNvencHevc => "hevc_nvenc",
                VideoEncoder.AmdAmfH264 => "h264_amf",
                VideoEncoder.IntelQsvH264 => "h264_qsv",
                VideoEncoder.SoftwareX264 => "libx264",
                VideoEncoder.SoftwareX265 => "libx265",
                VideoEncoder.SoftwareSvtAv1 => "libsvtav1",
                _ => "libx264"
            };
        }

        public async Task<bool> StartRecordingAsync(StudioProfile profile)
        {
            lock (_stateLock)
            {
                if (_isRecording) return false;
            }

            try
            {
                Directory.CreateDirectory(profile.RecordingDirectory);
                string ext = profile.RecFormat.ToString().ToLower();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                _currentOutputFilePath = Path.Combine(profile.RecordingDirectory, $"Ramaverse_{timestamp}.{ext}");

                int width = profile.CanvasWidth;
                int height = profile.CanvasHeight;
                int fps = profile.Fps;
                int videoBitrate = profile.RecordingBitrateKbps;
                int audioBitrate = profile.AudioBitrateKbps;

                string encoder = ResolveEncoderString(profile.Encoder);

                _audioPipeName = $"RamaverseAudio_{Guid.NewGuid():N}";
                _audioPipeServer = new NamedPipeServerStream(_audioPipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);

                string pipePath = $@"\\.\pipe\{_audioPipeName}";

                string args = $"-y -f rawvideo -pix_fmt bgra -s {width}x{height} -r {fps} -i - " +
                              $"-f s16le -ar 48000 -ac 2 -i \"{pipePath}\" " +
                              $"-c:v {encoder} -b:v {videoBitrate}k -preset veryfast -pix_fmt yuv420p " +
                              $"-c:a aac -b:a {audioBitrate}k \"{_currentOutputFilePath}\"";

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

                // Start async pump threads
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
                    _isRecording = true;
                    _isPaused = false;
                    _stopwatch.Restart();
                    _pauseOffsetMs = 0;
                }

                StartStatsMonitoring();
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to start recording: {ex.Message}");
                StopRecording();
                return false;
            }
        }

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

        public void WriteVideoFrame(byte[] pixelBytes)
        {
            lock (_stateLock)
            {
                if (!_isRecording || _isPaused || _videoQueue == null || _videoQueue.IsAddingCompleted) return;
            }

            try
            {
                _videoQueue.TryAdd(pixelBytes);
            }
            catch { }
        }

        public void WriteAudioSamples(byte[] audioBytes, int count)
        {
            lock (_stateLock)
            {
                if (!_isRecording || _isPaused || _audioQueue == null || _audioQueue.IsAddingCompleted) return;
            }

            try
            {
                byte[] chunk = new byte[count];
                Buffer.BlockCopy(audioBytes, 0, chunk, 0, count);
                _audioQueue.TryAdd(chunk);
            }
            catch { }
        }

        public void StopRecording()
        {
            lock (_stateLock)
            {
                if (!_isRecording) return;
                _isRecording = false;
                _isPaused = false;
                _stopwatch.Stop();
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

        private void StartStatsMonitoring()
        {
            _ = Task.Run(async () =>
            {
                while (_isRecording)
                {
                    await Task.Delay(500);
                    if (!_isRecording) break;

                    long bytesWritten = 0;
                    try
                    {
                        if (File.Exists(_currentOutputFilePath))
                        {
                            bytesWritten = new FileInfo(_currentOutputFilePath).Length;
                        }
                    }
                    catch { }

                    var stats = new RecordingStats
                    {
                        ElapsedTime = TimeSpan.FromMilliseconds(Math.Max(0, _stopwatch.ElapsedMilliseconds - _pauseOffsetMs)),
                        BytesWritten = bytesWritten,
                        IsPaused = _isPaused
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
