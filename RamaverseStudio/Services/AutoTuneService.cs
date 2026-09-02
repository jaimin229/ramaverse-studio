using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;

namespace RamaverseStudio.Services
{
    /// <summary>
    /// Hardware capability tier detected at launch. Every machine gets a
    /// configuration matched to what it can actually sustain, so powerful PCs
    /// run at full power and weak PCs stay smooth instead of stuttering.
    /// </summary>
    public enum PerformanceTier
    {
        Low,       // <=4 cores / <=8 GB RAM / no GPU encoder
        Medium,    // 6-10 cores or 8-16 GB RAM, some iGPU
        High,      // 12+ cores or 16 GB+, has NVENC/AMF/QSV
        Enthusiast // 16+ cores AND 24 GB+ RAM AND NVENC/HEVC-class GPU
    }

    public class HardwareProfile
    {
        public PerformanceTier Tier { get; init; }
        public int CoreCount { get; init; }
        public long TotalRamGb { get; init; }
        public string GpuName { get; init; } = "Unknown";
        public bool HasNvidiaEncoder { get; init; }
        public bool HasAmdEncoder { get; init; }
        public bool HasIntelQuickSync { get; init; }
        public string CpuName { get; init; } = "Unknown";
        public string Summary { get; init; } = "";
    }

    /// <summary>
    /// Auto-Performance: probes the machine once at launch and derives every
    /// engine tuning knob from a single source of truth. The MainWindow asks
    /// this service — no manual benchmarking, no settings to hunt.
    /// </summary>
    public static class AutoTuneService
    {
        public static HardwareProfile? Profile { get; private set; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX data);

        /// <summary>
        /// Runs the one-time hardware probe. Safe to call repeatedly; results
        /// are cached after the first run.
        /// </summary>
        public static HardwareProfile Detect()
        {
            if (Profile != null) return Profile;

            int cores = Math.Max(1, Environment.ProcessorCount);
            long ramGb = Math.Max(1, GetTotalRamGb());

            var (gpu, hasNv, hasAmd, hasQsv) = DetectGpu();

            // Tier rules: GPU encoder presence is the biggest quality lever,
            // then cores, then RAM headroom.
            PerformanceTier tier;
            if (cores >= 16 && ramGb >= 24 && hasNv) tier = PerformanceTier.Enthusiast;
            else if (cores >= 8 && (hasNv || hasAmd || hasQsv || ramGb >= 16)) tier = PerformanceTier.High;
            else if (cores >= 6 && ramGb >= 8) tier = PerformanceTier.Medium;
            else tier = PerformanceTier.Low;

            string cpu;
            try
            {
                cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? $"{cores} cores";
            }
            catch { cpu = $"{cores} cores"; }

            Profile = new HardwareProfile
            {
                Tier = tier,
                CoreCount = cores,
                TotalRamGb = ramGb,
                GpuName = gpu,
                HasNvidiaEncoder = hasNv,
                HasAmdEncoder = hasAmd,
                HasIntelQuickSync = hasQsv,
                CpuName = cpu,
                Summary = $"{cpu} • {cores} cores • {ramGb} GB RAM • {gpu} → {tier} tier"
            };
            return Profile;
        }

        private static long GetTotalRamGb()
        {
            try
            {
                var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref mem))
                {
                    return (long)Math.Round(mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0));
                }
            }
            catch { }
            return 8; // sensible default
        }

        /// <summary>
        /// GPU detection via the registry (no WMI cost at startup).
        /// Encoder availability is inferred from the vendor string.
        /// </summary>
        private static (string name, bool nv, bool amd, bool qsv) DetectGpu()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
                if (key == null) return ("Basic display adapter", false, false, false);

                string? best = null;
                foreach (var sub in key.GetSubKeyNames())
                {
                    using var dev = key.OpenSubKey(sub);
                    var desc = dev?.GetValue("DriverDesc") as string;
                    if (!string.IsNullOrWhiteSpace(desc))
                    {
                        best = desc; // keep the last (usually the active) adapter
                    }
                }

                string gpu = best ?? "Basic display adapter";
                bool nv = gpu.Contains("nvidia", StringComparison.OrdinalIgnoreCase);
                bool amd = gpu.Contains("amd", StringComparison.OrdinalIgnoreCase) ||
                           gpu.Contains("radeon", StringComparison.OrdinalIgnoreCase);
                bool qsv = gpu.Contains("intel", StringComparison.OrdinalIgnoreCase) ||
                           gpu.Contains("iris", StringComparison.OrdinalIgnoreCase) ||
                           gpu.Contains("uhd", StringComparison.OrdinalIgnoreCase);
                return (gpu, nv, amd, qsv);
            }
            catch
            {
                return ("Basic display adapter", false, false, false);
            }
        }

        // ---- Derived tuning knobs (single source of truth) ----

        /// <summary>Target canvas FPS — 60 on capable machines, 30 on Low.</summary>
        public static int TargetFps => Detect().Tier switch
        {
            PerformanceTier.Low => 30,
            _ => 60
        };

        /// <summary>Preview refresh decimation: show every frame on strong PCs.</summary>
        public static int PreviewDecimation => Detect().Tier switch
        {
            PerformanceTier.Enthusiast => 1, // 60fps preview
            PerformanceTier.High => 1,       // 60fps preview
            PerformanceTier.Medium => 2,     // 30fps preview
            _ => 2                           // 30fps preview
        };

        /// <summary>Replay-buffer length: more on machines with RAM headroom.</summary>
        public static int ReplayBufferSeconds => Detect().Tier switch
        {
            PerformanceTier.Enthusiast => 60,
            PerformanceTier.High => 30,
            PerformanceTier.Medium => 20,
            _ => 10
        };

        /// <summary>Recording bitrate ceiling by tier (still respects user override).</summary>
        public static int RecommendedRecordingBitrate => Detect().Tier switch
        {
            PerformanceTier.Enthusiast => 24000,
            PerformanceTier.High => 12000,
            PerformanceTier.Medium => 8000,
            _ => 5000
        };

        /// <summary>
        /// Best encoder for this machine: hardware when the GPU provides one,
        /// x264 with tuned threads otherwise. Falls back gracefully.
        /// </summary>
        public static Models.VideoEncoder RecommendedEncoder
        {
            get
            {
                var p = Detect();
                if (p.HasNvidiaEncoder) return Models.VideoEncoder.NvidiaNvencH264;
                if (p.HasAmdEncoder) return Models.VideoEncoder.AmdAmfH264;
                if (p.HasIntelQuickSync) return Models.VideoEncoder.IntelQsvH264;
                return Models.VideoEncoder.SoftwareX264;
            }
        }

        /// <summary>
        /// Application-level renderer thread priority guidance: strong machines
        /// push the compositor hard; weak machines keep it polite so the game
        /// the user is streaming keeps its own CPU.
        /// </summary>
        public static int RenderThreadPriority => Detect().Tier switch
        {
            PerformanceTier.Enthusiast => 4,  // AboveNormal+
            PerformanceTier.High => 3,         // AboveNormal
            PerformanceTier.Medium => 2,
            _ => 1                            // Normal — yield to the user's game
        };

        /// <summary>
        /// True on tier Low/Medium: cap expensive per-frame work (chroma key,
        /// color adjust) at a lower resolution to keep frame pacing.
        /// </summary>
        public static bool UseLightweightCapture => Detect().Tier <= PerformanceTier.Medium;

        /// <summary>
        /// UI update cadence: heavier panels refresh slower on weak machines.
        /// </summary>
        public static int UiTickDividerMs => Detect().Tier switch
        {
            PerformanceTier.Low => 66,    // ~15 Hz
            PerformanceTier.Medium => 50,
            _ => 33
        };
    }
}
