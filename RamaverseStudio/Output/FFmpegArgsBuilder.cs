using System;
using System.IO;
using System.Text;
using RamaverseStudio.Models;

namespace RamaverseStudio.Output
{
    /// <summary>
    /// Central, correct FFmpeg argument construction shared by the recording,
    /// streaming, and replay engines. All encoder-specific quirks live here so
    /// every pipeline behaves identically.
    /// </summary>
    public static class FFmpegArgsBuilder
    {
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

        /// <summary>
        /// Encoder-appropriate speed preset. libx264 accepts veryfast;
        /// NVENC/AMF/QSV do not and would fail to start.
        /// </summary>
        public static string ResolvePreset(VideoEncoder encoder)
        {
            return encoder switch
            {
                VideoEncoder.NvidiaNvencH264 => "-preset p4",
                VideoEncoder.NvidiaNvencHevc => "-preset p4",
                VideoEncoder.AmdAmfH264 => "-quality speed",
                VideoEncoder.IntelQsvH264 => "-preset veryfast",
                VideoEncoder.SoftwareX264 => "-preset veryfast",
                VideoEncoder.SoftwareX265 => "-preset ultrafast",
                VideoEncoder.SoftwareSvtAv1 => "-preset 8",
                _ => "-preset veryfast"
            };
        }

        /// <summary>
        /// Standard raw BGRA video + s16le audio input pair. The video comes from
        /// stdin; the audio comes from a named pipe supplied by the caller.
        /// </summary>
        public static void AppendRawInputs(StringBuilder args, int width, int height, int fps, string audioPipePath)
        {
            args.Append("-y -f rawvideo -pix_fmt bgra -s ").Append(width).Append('x').Append(height)
                .Append(" -r ").Append(fps).Append(" -i - ")
                .Append("-f s16le -ar 48000 -ac 2 -i \"").Append(audioPipePath).Append("\" ");
        }

        public static string BuildRecordingArgs(StudioProfile profile, int width, int height, int fps, string audioPipePath, string outputPath)
        {
            var args = new StringBuilder(512);
            AppendRawInputs(args, width, height, fps, audioPipePath);
            return BuildRecordingOutputArgs(profile, width, height, fps, args, outputPath);
        }

        /// <summary>
        /// Multi-track recording: mic and desktop arrive as separate audio pipes
        /// (inputs 1 and 2) and are muxed as distinct streams so editors can mix
        /// them again in post — the mixed master is NOT recorded in this mode.
        /// </summary>
        public static string BuildMultiTrackRecordingArgs(StudioProfile profile, int width, int height, int fps,
            string micPipePath, string desktopPipePath, string outputPath)
        {
            var args = new StringBuilder(640);
            AppendRawInputs(args, width, height, fps, micPipePath);
            args.Append("-f s16le -ar 48000 -ac 2 -i \"").Append(desktopPipePath).Append("\" ");

            // Map: 0 = video (stdin), 1 = mic audio, 2 = desktop audio
            args.Append("-map 0:v -map 1:a -map 2:a ");

            return BuildRecordingOutputArgs(profile, width, height, fps, args, outputPath,
                audioCodecSuffix: " -disposition:a:1 default -disposition:a:0 none");
        }

        private static string BuildRecordingOutputArgs(StudioProfile profile, int width, int height, int fps,
            StringBuilder args, string outputPath, string? audioCodecSuffix = null)
        {

            string encoder = ResolveEncoderString(profile.Encoder);
            string preset = ResolvePreset(profile.Encoder);

            // Lossless archival mode: mathematically lossless intra-only encode.
            // Massive files (bitrate knob is ignored) — for editors/archives.
            if (profile.LosslessRecording)
            {
                args.Append("-c:v libx264rgb -qp 0 -preset ultrafast ")
                    .Append("-g ").Append(Math.Max(12, fps * 2)).Append(" ");
            }
            else
            {
                args.Append("-c:v ").Append(encoder).Append(' ')
                    .Append(preset).Append(' ')
                    .Append("-b:v ").Append(profile.RecordingBitrateKbps).Append("k ");

                // GOP size twice the frame rate keeps seeking responsive in editors
                args.Append("-g ").Append(Math.Max(12, fps * 2)).Append(" ");
            }

            // AMF requires explicit usage; everything else tolerates it
            if (profile.Encoder == VideoEncoder.AmdAmfH264)
            {
                args.Append("-usage transcoding ");
            }

            args.Append("-pix_fmt yuv420p ")
                .Append("-c:a aac -b:a ").Append(profile.AudioBitrateKbps).Append("k ")
                .Append("-ar 48000 ");

            // Multi-track streams carry an explicit default track so players
            // pick the mic channel, not the desktop channel, on open.
            if (!string.IsNullOrEmpty(audioCodecSuffix))
            {
                args.Append(audioCodecSuffix.Trim()).Append(' ');
            }

            // Crash-proof recording: unless the user explicitly chose MOV/WebM,
            // we always capture to MKV (index-at-end-free container). On stop we
            // remux to the user's chosen container. If the app crashes, the MKV
            // is still 100% playable — the exact OBS safety model.
            bool captureToMkv = profile.RecFormat != RecordingFormat.MKV;
            string capturePath = captureToMkv
                ? Path.ChangeExtension(outputPath, ".mkv")
                : outputPath;

            if (captureToMkv)
            {
                args.Append("-f matroska ");
            }

            args.Append("\"").Append(capturePath).Append("\"");

            return args.ToString();
        }

        /// <summary>
        /// True when BuildRecordingArgs captured to a crash-proof MKV that
        /// needs remuxing to the user's chosen container on stop.
        /// </summary>
        public static bool UsesMkvSafetyCapture(StudioProfile profile) =>
            profile.RecFormat != RecordingFormat.MKV;

        /// <summary>
        /// Arguments for a stream-copy remux (no re-encode) from the crash-proof
        /// MKV capture to the user's chosen container.
        /// </summary>
        public static string BuildRemuxArgs(string mkvPath, string targetPath) =>
            $"-y -i \"{mkvPath}\" -c copy \"{targetPath}\"";

        /// <summary>
        /// Arguments for an FFmpeg segment muxer: one output file per N seconds
        /// of recording, so a 4-hour stream session produces many small, safe
        /// files instead of one giant fragile one.
        /// </summary>
        public static string BuildSegmentedRecordingArgs(StudioProfile profile, int width, int height, int fps, string audioPipePath, string outputPattern, int segmentSeconds)
        {
            var args = new StringBuilder(640);
            AppendRawInputs(args, width, height, fps, audioPipePath);

            string encoder = ResolveEncoderString(profile.Encoder);
            string preset = ResolvePreset(profile.Encoder);

            args.Append("-c:v ").Append(encoder).Append(' ')
                .Append(preset).Append(' ')
                .Append("-b:v ").Append(profile.RecordingBitrateKbps).Append("k ")
                .Append("-g ").Append(Math.Max(12, fps * 2)).Append(" ");

            if (profile.Encoder == VideoEncoder.AmdAmfH264)
            {
                args.Append("-usage transcoding ");
            }

            args.Append("-pix_fmt yuv420p ")
                .Append("-c:a aac -b:a ").Append(profile.AudioBitrateKbps).Append("k ")
                .Append("-ar 48000 ")
                .Append("-f segment ")
                .Append("-segment_time ").Append(Math.Max(60, segmentSeconds)).Append(' ')
                .Append("-reset_timestamps 1 ")
                .Append("\"").Append(outputPattern).Append("\"");

            return args.ToString();
        }

        public static string BuildStreamArgs(StudioProfile profile, int width, int height, int fps, string audioPipePath, string targetUrl, string? videoFilter)
        {
            var args = new StringBuilder(512);
            AppendRawInputs(args, width, height, fps, audioPipePath);

            int videoBitrate = profile.StreamBitrateKbps;

            args.Append("-c:v ").Append(ResolveEncoderString(profile.Encoder)).Append(' ')
                .Append(ResolvePreset(profile.Encoder)).Append(' ')
                .Append("-b:v ").Append(videoBitrate).Append("k ")
                .Append("-maxrate ").Append(videoBitrate).Append("k ")
                .Append("-bufsize ").Append(videoBitrate * 2).Append("k ")
                .Append("-g ").Append(Math.Max(12, fps * 2)).Append(" ");

            if (profile.Encoder == VideoEncoder.AmdAmfH264)
            {
                args.Append("-usage transcoding ");
            }

            if (!string.IsNullOrWhiteSpace(videoFilter))
            {
                args.Append("-vf \"").Append(videoFilter).Append("\" ");
            }

            args.Append("-pix_fmt yuv420p ")
                .Append("-c:a aac -b:a ").Append(Math.Min(160, Math.Max(96, profile.StreamAudioBitrateKbps))).Append("k ")
                .Append("-ar 48000 ");

            if (targetUrl.StartsWith("srt://", StringComparison.OrdinalIgnoreCase))
            {
                args.Append("-f mpegts \"").Append(targetUrl).Append("\"");
            }
            else
            {
                args.Append("-f flv \"").Append(targetUrl).Append("\"");
            }

            return args.ToString();
        }
    }
}
