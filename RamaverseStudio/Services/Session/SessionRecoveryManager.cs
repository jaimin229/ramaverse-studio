using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RamaverseStudio.Output.Recovery;

namespace RamaverseStudio.Services.Session
{
    public class ActiveSessionJournal
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
        public int ProcessId { get; set; } = Environment.ProcessId;
        public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
        public string ActiveMkvPath { get; set; } = "";
        public string TargetOutputPath { get; set; } = "";
    }

    /// <summary>
    /// Cold-Startup Session Recovery Manager.
    /// Uses journal lockfiles and heuristic scanning to detect crashed recording sessions
    /// and facilitate 1-click lossless file reconstruction.
    /// </summary>
    public class SessionRecoveryManager : IDisposable
    {
        private readonly string _journalPath;
        private readonly Timer _heartbeatTimer;
        private ActiveSessionJournal? _currentJournal;
        private readonly MatroskaRecoveryService _recoveryService;

        public SessionRecoveryManager(MatroskaRecoveryService recoveryService)
        {
            _recoveryService = recoveryService;
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamaverseStudio");
            Directory.CreateDirectory(appData);
            _journalPath = Path.Combine(appData, "active_session.journal");
            _heartbeatTimer = new Timer(OnHeartbeatTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        public void BeginSessionTracking(ActiveSessionJournal journal)
        {
            _currentJournal = journal;
            PersistJournal();
            _heartbeatTimer.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public void EndSessionCleanly()
        {
            _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _currentJournal = null;
            try
            {
                if (File.Exists(_journalPath))
                    File.Delete(_journalPath);
            }
            catch { }
        }

        private void OnHeartbeatTick(object? state)
        {
            if (_currentJournal == null) return;
            _currentJournal.LastHeartbeatUtc = DateTime.UtcNow;
            PersistJournal();
        }

        private void PersistJournal()
        {
            if (_currentJournal == null) return;
            try
            {
                string json = JsonSerializer.Serialize(_currentJournal);
                string tempPath = _journalPath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _journalPath, true);
            }
            catch { }
        }

        public async Task<List<ActiveSessionJournal>> DetectCrashedSessionsAsync(string defaultRecordingDir)
        {
            var crashedSessions = new List<ActiveSessionJournal>();

            if (File.Exists(_journalPath))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(_journalPath);
                    var journal = JsonSerializer.Deserialize<ActiveSessionJournal>(json);
                    if (journal != null && File.Exists(journal.ActiveMkvPath))
                    {
                        crashedSessions.Add(journal);
                    }
                }
                catch { }
            }

            if (Directory.Exists(defaultRecordingDir))
            {
                var mkvFiles = Directory.GetFiles(defaultRecordingDir, "Ramaverse_*.mkv");
                foreach (var mkv in mkvFiles)
                {
                    string matchingMp4 = Path.ChangeExtension(mkv, ".mp4");
                    if (!File.Exists(matchingMp4) && _recoveryService.IsValidMatroskaHeader(mkv))
                    {
                        if (!crashedSessions.Exists(s => s.ActiveMkvPath.Equals(mkv, StringComparison.OrdinalIgnoreCase)))
                        {
                            var info = new FileInfo(mkv);
                            if (info.Length > 1024 * 32)
                            {
                                crashedSessions.Add(new ActiveSessionJournal
                                {
                                    SessionId = "OrphanedCapture",
                                    ActiveMkvPath = mkv,
                                    TargetOutputPath = matchingMp4,
                                    StartTimeUtc = info.CreationTimeUtc,
                                    LastHeartbeatUtc = info.LastWriteTimeUtc
                                });
                            }
                        }
                    }
                }
            }

            return crashedSessions;
        }

        public void Dispose()
        {
            _heartbeatTimer.Dispose();
        }
    }
}
