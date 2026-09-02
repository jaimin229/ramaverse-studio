using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RamaverseStudio.Services
{
    public class ChapterMarker
    {
        public TimeSpan Timestamp { get; init; }
        public string Title { get; init; } = "";
        public ChapterMarkerService.MarkerKind Kind { get; init; }
        public string Color { get; init; } = "Cyan";
    }

    /// <summary>
    /// Professional Chapter Marker & NLE Timeline Marker Service.
    /// Exports YouTube-compliant *_chapters.txt files and DaVinci Resolve / Premiere Pro
    /// timeline markers (*_davinci_markers.csv) automatically at recording end.
    /// </summary>
    public class ChapterMarkerService
    {
        public enum MarkerKind
        {
            Manual,
            SceneSwitch,
            AutoClip,
            SessionStart,
            SessionEnd
        }

        private readonly List<ChapterMarker> _markers = new();
        private readonly object _lock = new();
        private DateTimeOffset _sessionStartUtc;

        public IReadOnlyList<ChapterMarker> Markers
        {
            get { lock (_lock) return _markers.ToList(); }
        }

        public void StartSession(string title = "Stream Intro")
        {
            lock (_lock)
            {
                _markers.Clear();
                _sessionStartUtc = DateTimeOffset.UtcNow;
                _markers.Add(new ChapterMarker
                {
                    Timestamp = TimeSpan.Zero,
                    Title = title,
                    Kind = MarkerKind.SessionStart,
                    Color = "Green"
                });
            }
        }

        public void AddMarker(string title, MarkerKind kind = MarkerKind.Manual)
        {
            lock (_lock)
            {
                var elapsed = DateTimeOffset.UtcNow - _sessionStartUtc;
                string color = kind switch
                {
                    MarkerKind.AutoClip => "Red",
                    MarkerKind.SceneSwitch => "Blue",
                    _ => "Cyan"
                };

                _markers.Add(new ChapterMarker
                {
                    Timestamp = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed,
                    Title = string.IsNullOrWhiteSpace(title) ? "Chapter Marker" : title,
                    Kind = kind,
                    Color = color
                });
            }
        }

        public void Add(string title, MarkerKind kind = MarkerKind.Manual) => AddMarker(title, kind);
        public void Add(string title, string kind) => AddMarker(title, MarkerKind.Manual);

        public static string FormatTimestamp(TimeSpan t) => FormatYouTubeTime(t);

        public string? ExportEventJson(string targetPathOrDir)
        {
            lock (_lock)
            {
                string dir = Directory.Exists(targetPathOrDir) ? targetPathOrDir : (Path.GetDirectoryName(targetPathOrDir) ?? "");
                string fileName = Directory.Exists(targetPathOrDir) ? "stream_events.json" : $"{Path.GetFileNameWithoutExtension(targetPathOrDir)}_events.json";
                string jsonFile = Path.Combine(dir, fileName);

                var payload = new
                {
                    markers = _markers.Select(m => new
                    {
                        timestamp = FormatYouTubeTime(m.Timestamp),
                        title = m.Title,
                        kind = m.Kind.ToString(),
                        color = m.Color
                    }).ToList()
                };

                string json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonFile, json);
                return jsonFile;
            }
        }

        /// <summary>
        /// Exports YouTube-compliant chapters file:
        /// 1. First timestamp is strictly 0:00
        /// 2. Minimum 3 chapters
        /// 3. Minimum 10 seconds between chapters
        /// </summary>
        public string? ExportYouTubeChapters(string videoFilePath)
        {
            lock (_lock)
            {
                if (_markers.Count == 0) return null;

                var sorted = _markers.OrderBy(m => m.Timestamp).ToList();
                var deduped = new List<ChapterMarker> { sorted[0] };

                foreach (var m in sorted.Skip(1))
                {
                    if ((m.Timestamp - deduped[^1].Timestamp).TotalSeconds >= 10)
                    {
                        deduped.Add(m);
                    }
                }

                // YouTube requires at least 3 chapters
                while (deduped.Count < 3)
                {
                    var lastTs = deduped[^1].Timestamp + TimeSpan.FromSeconds(15);
                    deduped.Add(new ChapterMarker
                    {
                        Timestamp = lastTs,
                        Title = $"Highlight {deduped.Count}",
                        Kind = MarkerKind.Manual,
                        Color = "Cyan"
                    });
                }

                string dir = Path.GetDirectoryName(videoFilePath) ?? "";
                string stem = Path.GetFileNameWithoutExtension(videoFilePath);
                string chapterFile = Path.Combine(dir, $"{stem}_chapters.txt");

                var lines = deduped.Select(m => $"{FormatYouTubeTime(m.Timestamp)} {m.Title}");
                File.WriteAllLines(chapterFile, lines);
                return chapterFile;
            }
        }

        /// <summary>
        /// Exports DaVinci Resolve Timeline Markers (.csv) for instant import in Fairlight / Edit page.
        /// </summary>
        public string? ExportDaVinciMarkersCsv(string videoFilePath, int fps = 60)
        {
            lock (_lock)
            {
                if (_markers.Count == 0) return null;

                string dir = Path.GetDirectoryName(videoFilePath) ?? "";
                string stem = Path.GetFileNameWithoutExtension(videoFilePath);
                string csvFile = Path.Combine(dir, $"{stem}_davinci_markers.csv");

                var sb = new StringBuilder();
                sb.AppendLine("Source In,Source Out,Record In,Record Out,Marker Name,Marker Note,Marker Color");

                foreach (var m in _markers.OrderBy(m => m.Timestamp))
                {
                    string tc = FormatTimecode(m.Timestamp, fps);
                    sb.AppendLine($"{tc},{tc},{tc},{tc},{EscapeCsv(m.Title)},{m.Kind},{m.Color}");
                }

                File.WriteAllText(csvFile, sb.ToString());
                return csvFile;
            }
        }

        public static string FormatYouTubeTime(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours}:{t.Minutes:D2}:{t.Seconds:D2}"
                : $"{t.Minutes}:{t.Seconds:D2}";
        }

        public static string FormatTimecode(TimeSpan t, int fps)
        {
            int frames = (int)((t.TotalSeconds - Math.Truncate(t.TotalSeconds)) * fps);
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}:{frames:D2}";
        }

        private static string EscapeCsv(string s) => $"\"{s.Replace("\"", "\"\"")}\"";
    }
}
