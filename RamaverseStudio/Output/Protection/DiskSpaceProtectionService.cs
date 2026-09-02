using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RamaverseStudio.Output.Protection
{
    public enum DiskSpaceSeverity
    {
        Healthy,
        WarningLowSpace,      // < 5.0 GB
        CriticalAutoPause,    // < 2.0 GB
        EmergencyShutdown     // < 500 MB
    }

    public class DiskSpaceEventArgs : EventArgs
    {
        public DiskSpaceSeverity Severity { get; }
        public ulong FreeBytesAvailable { get; }
        public ulong TotalBytes { get; }
        public double FreeGigabytes => FreeBytesAvailable / (1024.0 * 1024.0 * 1024.0);
        public string TargetDirectory { get; }

        public DiskSpaceEventArgs(DiskSpaceSeverity severity, ulong freeBytes, ulong totalBytes, string dir)
        {
            Severity = severity;
            FreeBytesAvailable = freeBytes;
            TotalBytes = totalBytes;
            TargetDirectory = dir;
        }
    }

    /// <summary>
    /// Proactive Win32 Disk Space Protection Service.
    /// Monitors NVMe/SSD drive capacity and prevents corrupt zero-byte pipe closures
    /// through a 3-tier safety threshold protocol.
    /// </summary>
    public class DiskSpaceProtectionService : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        private CancellationTokenSource? _cts;
        private Task? _monitorTask;
        private string _targetDirectory = "";
        private DiskSpaceSeverity _currentSeverity = DiskSpaceSeverity.Healthy;

        public ulong WarningThresholdBytes { get; set; } = 5UL * 1024 * 1024 * 1024;    // 5 GB
        public ulong AutoPauseThresholdBytes { get; set; } = 2UL * 1024 * 1024 * 1024;  // 2 GB
        public ulong EmergencyThresholdBytes { get; set; } = 500UL * 1024 * 1024;       // 500 MB

        public event EventHandler<DiskSpaceEventArgs>? SeverityChanged;
        public event EventHandler<DiskSpaceEventArgs>? PeriodicStatusUpdated;

        public void StartMonitoring(string targetDirectory, int pollingIntervalMs = 1500)
        {
            StopMonitoring();
            _targetDirectory = targetDirectory;
            _cts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => PollingLoopAsync(pollingIntervalMs, _cts.Token));
        }

        public void StopMonitoring()
        {
            _cts?.Cancel();
            try { _monitorTask?.Wait(1000); } catch { }
            _cts?.Dispose();
            _cts = null;
        }

        private async Task PollingLoopAsync(int intervalMs, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(_targetDirectory) && Directory.Exists(_targetDirectory))
                    {
                        if (GetDiskFreeSpaceEx(_targetDirectory, out ulong freeBytes, out ulong totalBytes, out _))
                        {
                            DiskSpaceSeverity newSeverity = EvaluateSeverity(freeBytes);
                            var args = new DiskSpaceEventArgs(newSeverity, freeBytes, totalBytes, _targetDirectory);

                            if (newSeverity != _currentSeverity)
                            {
                                _currentSeverity = newSeverity;
                                SeverityChanged?.Invoke(this, args);
                            }

                            PeriodicStatusUpdated?.Invoke(this, args);
                        }
                    }
                }
                catch { }

                try
                {
                    await Task.Delay(intervalMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        private DiskSpaceSeverity EvaluateSeverity(ulong freeBytes)
        {
            if (freeBytes < EmergencyThresholdBytes)
                return DiskSpaceSeverity.EmergencyShutdown;
            if (freeBytes < AutoPauseThresholdBytes)
                return DiskSpaceSeverity.CriticalAutoPause;
            if (freeBytes < WarningThresholdBytes)
                return DiskSpaceSeverity.WarningLowSpace;

            return DiskSpaceSeverity.Healthy;
        }

        public void Dispose()
        {
            StopMonitoring();
        }
    }
}
