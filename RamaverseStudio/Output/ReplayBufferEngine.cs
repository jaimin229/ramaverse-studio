using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using RamaverseStudio.Models;

namespace RamaverseStudio.Output
{
    public class ReplayBufferEngine : IDisposable
    {
        private readonly int _bufferSeconds;
        private readonly LinkedList<byte[]> _videoFrames = new LinkedList<byte[]>();
        private readonly LinkedList<byte[]> _audioChunks = new LinkedList<byte[]>();
        private readonly object _bufferLock = new object();

        private int _maxVideoFrames = 1800; // 30 sec @ 60 FPS
        private int _canvasWidth = 1920;
        private int _canvasHeight = 1080;
        private int _fps = 60;
        private bool _isEnabled = true;

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
            _bufferSeconds = bufferSeconds;
            _maxVideoFrames = bufferSeconds * 60;
        }

        public void SetFormat(int width, int height, int fps)
        {
            lock (_bufferLock)
            {
                _canvasWidth = width;
                _canvasHeight = height;
                _fps = fps;
                _maxVideoFrames = _bufferSeconds * fps;
                _videoFrames.Clear();
            }
        }

        public void PushVideoFrame(byte[] bgraPixels)
        {
            if (!_isEnabled) return;

            byte[] frameCopy = new byte[bgraPixels.Length];
            Buffer.BlockCopy(bgraPixels, 0, frameCopy, 0, bgraPixels.Length);

            lock (_bufferLock)
            {
                _videoFrames.AddLast(frameCopy);
                while (_videoFrames.Count > _maxVideoFrames)
                {
                    _videoFrames.RemoveFirst();
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
                _audioChunks.AddLast(chunk);
                // 30 seconds of 48kHz 16-bit stereo = 30 * 48000 * 4 = 5,760,000 bytes (~5.7 MB)
                int maxAudioBytes = _bufferSeconds * 48000 * 4;
                int currentBytes = 0;
                var node = _audioChunks.Last;
                while (node != null)
                {
                    currentBytes += node.Value.Length;
                    if (currentBytes > maxAudioBytes)
                    {
                        while (_audioChunks.First != node)
                        {
                            _audioChunks.RemoveFirst();
                        }
                        break;
                    }
                    node = node.Previous;
                }
            }
        }

        public async Task<string?> SaveReplayAsync(string destinationFolder, VideoEncoder encoder = VideoEncoder.AutoHardware, bool isVertical = false)
        {
            List<byte[]> framesToSave;
            List<byte[]> audioToSave;

            lock (_bufferLock)
            {
                if (_videoFrames.Count == 0) return null;
                framesToSave = new List<byte[]>(_videoFrames);
                audioToSave = new List<byte[]>(_audioChunks);
            }

            try
            {
                Directory.CreateDirectory(destinationFolder);
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string outPath = Path.Combine(destinationFolder, $"Replay_{(isVertical ? "Short_" : "")}{timestamp}.mp4");

                string encStr = FFmpegRecordingEngine.ResolveEncoderString(encoder);
                string pipeName = $"RamaverseReplayAudio_{Guid.NewGuid():N}";
                using var audioPipeServer = new NamedPipeServerStream(pipeName, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 65536, 65536);

                string pipePath = $@"\\.\pipe\{pipeName}";
                string vfFilter = isVertical ? "-vf \"crop=ih*9/16:ih:(iw-ih*9/16)/2:0,scale=1080:1920\" " : "";

                string args = $"-y -f rawvideo -pix_fmt bgra -s {_canvasWidth}x{_canvasHeight} -r {_fps} -i - " +
                              $"-f s16le -ar 48000 -ac 2 -i \"{pipePath}\" " +
                              $"{vfFilter}" +
                              $"-c:v {encStr} -b:v 16000k -preset ultrafast -pix_fmt yuv420p " +
                              $"-c:a aac -b:a 192k \"{outPath}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = FFmpegPathResolver.GetFFmpegPath(),
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi };
                proc.Start();

                var connectTask = audioPipeServer.WaitForConnectionAsync();
                await Task.WhenAny(connectTask, Task.Delay(2000));

                // Write video frames
                var videoTask = Task.Run(() =>
                {
                    using var vStream = proc.StandardInput.BaseStream;
                    foreach (var f in framesToSave)
                    {
                        vStream.Write(f, 0, f.Length);
                    }
                    vStream.Flush();
                });

                // Write audio chunks
                var audioTask = Task.Run(() =>
                {
                    if (audioPipeServer.IsConnected)
                    {
                        foreach (var a in audioToSave)
                        {
                            audioPipeServer.Write(a, 0, a.Length);
                        }
                        audioPipeServer.Flush();
                    }
                });

                await Task.WhenAll(videoTask, audioTask);
                proc.WaitForExit(4000);

                return File.Exists(outPath) ? outPath : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Replay buffer failed: {ex.Message}");
                return null;
            }
        }

        public void Clear()
        {
            lock (_bufferLock)
            {
                _videoFrames.Clear();
                _audioChunks.Clear();
            }
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
