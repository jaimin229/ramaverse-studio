using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using RamaverseStudio.Models;
using RamaverseStudio.Video;

namespace RamaverseStudio.Output
{
    /// <summary>
    /// Rolling instant-replay buffer. Frames are reference-counted pool objects,
    /// so memory usage is bounded by the pool rather than by raw byte arrays.
    /// 30s @ 1080p60 ≈ 180 frames × 8.3 MB ≈ 1.5 GB worst case for the pool;
    /// the pool naturally reuses the same ~180 frames instead of allocating 1800.
    /// </summary>
    public class ReplayBufferEngine : IDisposable
    {
        private readonly int _bufferSeconds;
        private readonly ConcurrentQueue<SharedFrame> _videoFrames = new();
        private readonly ConcurrentQueue<byte[]> _audioChunks = new();
        private readonly object _bufferLock = new object();
        private readonly VideoFramePool _pool = new();

        private int _maxVideoFrames = 1800;
        private int _canvasWidth = 1920;
        private int _canvasHeight = 1080;
        private int _fps = 60;
        private bool _isEnabled = true;
        private int _audioBytesQueued = 0;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (!value) Clear();
            }
        }

        public ReplayBufferEngine(int bufferSeconds = 30)
        {
            _bufferSeconds = Math.Clamp(bufferSeconds, 5, 120);
            // Constructor-time cap assumes 60fps until SetFormat runs; SetFormat
            // recomputes from the real canvas FPS.
            _maxVideoFrames = _bufferSeconds * 60;
        }

        public void SetFormat(int width, int height, int fps)
        {
            lock (_bufferLock)
            {
                _canvasWidth = width;
                _canvasHeight = height;
                _fps = Math.Max(1, fps);
                _maxVideoFrames = _bufferSeconds * _fps;

                // Hard memory ceiling: never let the buffer exceed a fraction of
                // physical RAM regardless of resolution or requested duration.
                long frameBytes = (long)width * height * 4;
                long budgetBytes = GetMemoryBudgetBytes();
                int maxByMemory = frameBytes > 0
                    ? (int)Math.Max(30, budgetBytes / frameBytes)
                    : _maxVideoFrames;

                if (maxByMemory < _maxVideoFrames)
                {
                    _maxVideoFrames = maxByMemory;
                }

                ClearLocked();
            }
        }

        /// <summary>
        /// Replay budget by machine tier. These caps keep the always-on buffer
        /// polite: instant-replay stays usable on every tier without the
        /// studio idling at multiple GB on laptops.
        /// </summary>
        private static long GetMemoryBudgetBytes() => RamaverseStudio.Services.AutoTuneService.Detect().Tier switch
        {
            RamaverseStudio.Services.PerformanceTier.Enthusiast => 1536L * 1024 * 1024, // 1.5 GB
            RamaverseStudio.Services.PerformanceTier.High => 768L * 1024 * 1024,         // 768 MB
            RamaverseStudio.Services.PerformanceTier.Medium => 384L * 1024 * 1024,     // 384 MB
            _ => 192L * 1024 * 1024                                                       // 192 MB
        };

        /// <summary>
        /// Takes ownership of the caller's reference to this frame.
        /// The frame is returned to the shared pool when it ages out or after save.
        /// </summary>
        public void PushVideoFrame(SharedFrame frame)
        {
            if (!_isEnabled)
            {
                frame.Release();
                return;
            }

            lock (_bufferLock)
            {
                _videoFrames.Enqueue(frame);
                if (_videoFrames.Count > _maxVideoFrames && _videoFrames.TryDequeue(out var oldest))
                {
                    oldest.Release();
                }
            }
        }

        public void PushAudioSamples(byte[] pcmBytes, int length)
        {
            if (!_isEnabled) return;

            byte[] chunk = new byte[length];
            Buffer.BlockCopy(pcmBytes, 0, chunk, 0, length);

            lock (_bufferLock)
            {
                _audioChunks.Enqueue(chunk);
                _audioBytesQueued += length;

                // 30 seconds of 48kHz 16-bit stereo = 5,760,000 bytes
                int maxAudioBytes = _bufferSeconds * 48000 * 4;
                while (_audioBytesQueued > maxAudioBytes && _audioChunks.TryDequeue(out var dropped))
                {
                    _audioBytesQueued -= dropped.Length;
                }
            }
        }

        public async Task<string?> SaveReplayAsync(string destinationFolder, VideoEncoder encoder = VideoEncoder.AutoHardware, bool isVertical = false)
        {
            List<SharedFrame> framesToSave;
            List<byte[]> audioToSave;

            lock (_bufferLock)
            {
                if (_videoFrames.Count == 0) return null;
                framesToSave = new List<SharedFrame>(_videoFrames);
                audioToSave = new List<byte[]>(_audioChunks);

                // The buffer keeps ownership; take extra refs for the save task.
                foreach (var f in framesToSave) f.AddRef();
            }

            try
            {
                // Release our save-refs on every exit path.
                try
                {
                    if (framesToSave.Count == 0) return null;

                    if (!FFmpegPathResolver.TryGetRealPath(out _))
                    {
                        Debug.WriteLine("Replay save skipped: FFmpeg not found.");
                        return null;
                    }

                    Directory.CreateDirectory(destinationFolder);
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    string outPath = Path.Combine(destinationFolder, $"Replay_{(isVertical ? "Short_" : "")}{timestamp}.mp4");

                    string encStr = FFmpegArgsBuilder.ResolveEncoderString(encoder);
                    string pipeName = $"RamaverseReplayAudio_{Guid.NewGuid():N}";
                    using var audioPipeServer = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);

                    string pipePath = $@"\\.\pipe\{pipeName}";

                    var sb = new StringBuilder(512);
                    FFmpegArgsBuilder.AppendRawInputs(sb, _canvasWidth, _canvasHeight, Math.Max(1, _fps), pipePath);
                    string preset = FFmpegArgsBuilder.ResolvePreset(encoder);
                    string vfFilter = isVertical ? $"-vf \"crop=ih*9/16:ih:(iw-ih*9/16)/2:0,scale=1080:1920:flags=fast_bilinear\" " : "";

                    string args = sb.ToString() +
                                  $"-c:v {encStr} {preset} -b:v 16000k -pix_fmt yuv420p " +
                                  vfFilter +
                                  $"-c:a aac -b:a 192k \"{outPath}\"";

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

                    using var proc = new Process { StartInfo = psi };
                    proc.Start();

                    var connectTask = audioPipeServer.WaitForConnectionAsync();
                    await Task.WhenAny(connectTask, Task.Delay(3000));

                    // Write video frames
                    var videoTask = Task.Run(() =>
                    {
                        using var vStream = proc.StandardInput.BaseStream;
                        foreach (var f in framesToSave)
                        {
                            vStream.Write(f.Pixels, 0, f.Height * f.Stride);
                        }
                        vStream.Flush();
                    });

                    // Write audio chunks
                    var audioTask = Task.Run(async () =>
                    {
                        if (audioPipeServer.IsConnected)
                        {
                            foreach (var a in audioToSave)
                            {
                                await audioPipeServer.WriteAsync(a, 0, a.Length);
                            }
                            await audioPipeServer.FlushAsync();
                        }
                    });

                    await Task.WhenAll(videoTask, audioTask);
                    proc.StandardInput.Close();
                    proc.WaitForExit(15000);

                    return File.Exists(outPath) ? outPath : null;
                }
                finally
                {
                    foreach (var f in framesToSave) f.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Replay buffer failed: {ex.Message}");
                return null;
            }
        }

        private void ClearLocked()
        {
            while (_videoFrames.TryDequeue(out var f)) f.Release();
            while (_audioChunks.TryDequeue(out _)) { }
            _audioBytesQueued = 0;
        }

        public void Clear()
        {
            lock (_bufferLock)
            {
                ClearLocked();
            }
        }

        public void Dispose()
        {
            IsEnabled = false;
        }
    }
}
