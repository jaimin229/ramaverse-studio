using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace RamaverseStudio.Services.Telemetry
{
    public struct FrameMetricEntry
    {
        public long TimestampTicks;
        public long FrameIndex;
        public double RenderTimeMs;
        public int QueueDepth;
        public int DroppedFrames;
        public double BitrateKbps;
    }

    /// <summary>
    /// Lock-Free Forensic Blackbox Telemetry Recorder.
    /// Captures ring-buffered performance metrics, FFmpeg logs, and persists
    /// structured JSON crash bundles upon unhandled application crash.
    /// </summary>
    public class BlackboxTelemetryService
    {
        private readonly FrameMetricEntry[] _metricRingBuffer;
        private int _ringBufferIndex = -1;
        private readonly int _bufferCapacity;
        private readonly ConcurrentQueue<string> _diagnosticLogs = new();
        private readonly ConcurrentQueue<string> _ffmpegStderrRing = new();
        private readonly string _telemetryDirectory;
        private readonly Stopwatch _uptimeStopwatch = Stopwatch.StartNew();

        public static BlackboxTelemetryService Instance { get; } = new BlackboxTelemetryService();

        public BlackboxTelemetryService(int capacity = 5000)
        {
            _bufferCapacity = capacity;
            _metricRingBuffer = new FrameMetricEntry[capacity];
            _telemetryDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RamaverseStudio",
                "blackbox_telemetry"
            );
            Directory.CreateDirectory(_telemetryDirectory);
        }

        public void InitializeGlobalExceptionTraps()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                RecordFatalCrash("AppDomain.UnhandledException", e.ExceptionObject as Exception);
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                RecordFatalCrash("TaskScheduler.UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TrackFrame(long frameIndex, double renderTimeMs, int queueDepth, int droppedFrames, double bitrateKbps)
        {
            int index = Interlocked.Increment(ref _ringBufferIndex);
            int slot = (int)((uint)index % (uint)_bufferCapacity);

            _metricRingBuffer[slot] = new FrameMetricEntry
            {
                TimestampTicks = Stopwatch.GetTimestamp(),
                FrameIndex = frameIndex,
                RenderTimeMs = renderTimeMs,
                QueueDepth = queueDepth,
                DroppedFrames = droppedFrames,
                BitrateKbps = bitrateKbps
            };
        }

        public void LogEvent(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            string stamped = $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}";
            _diagnosticLogs.Enqueue(stamped);
            while (_diagnosticLogs.Count > 500) _diagnosticLogs.TryDequeue(out _);
        }

        public void AppendFfmpegStderr(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _ffmpegStderrRing.Enqueue(line);
            while (_ffmpegStderrRing.Count > 500) _ffmpegStderrRing.TryDequeue(out _);
        }

        public string RecordFatalCrash(string faultOrigin, Exception? exception)
        {
            try
            {
                string crashId = $"crash_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
                string dumpPath = Path.Combine(_telemetryDirectory, $"{crashId}.json");

                var crashPayload = new
                {
                    CrashId = crashId,
                    TimestampUtc = DateTime.UtcNow,
                    Uptime = _uptimeStopwatch.Elapsed.ToString(),
                    FaultOrigin = faultOrigin,
                    Exception = exception != null ? new
                    {
                        Type = exception.GetType().FullName,
                        Message = exception.Message,
                        StackTrace = exception.StackTrace
                    } : null,
                    SystemInfo = new
                    {
                        OSVersion = Environment.OSVersion.ToString(),
                        Is64BitOS = Environment.Is64BitOperatingSystem,
                        ProcessorCount = Environment.ProcessorCount,
                        WorkingSetMB = Environment.WorkingSet / (1024 * 1024)
                    },
                    RecentFfmpegLogs = _ffmpegStderrRing.ToArray(),
                    RecentEvents = _diagnosticLogs.ToArray(),
                    RecentFrameMetricsSample = ExtractRecentMetrics(100)
                };

                string json = JsonSerializer.Serialize(crashPayload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dumpPath, json, Encoding.UTF8);
                return dumpPath;
            }
            catch
            {
                return string.Empty;
            }
        }

        private FrameMetricEntry[] ExtractRecentMetrics(int count)
        {
            int currentIndex = Volatile.Read(ref _ringBufferIndex);
            if (currentIndex < 0) return Array.Empty<FrameMetricEntry>();

            int samples = Math.Min(count, Math.Min(currentIndex + 1, _bufferCapacity));
            var results = new FrameMetricEntry[samples];

            for (int i = 0; i < samples; i++)
            {
                int targetIndex = currentIndex - (samples - 1 - i);
                int slot = (int)((uint)targetIndex % (uint)_bufferCapacity);
                results[i] = _metricRingBuffer[slot];
            }

            return results;
        }
    }
}
