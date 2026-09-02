# RAMAVERSE STUDIO

> **Record. Stream. Create. Your complete native creator studio in one place.**

[![Build & Release](https://github.com/jaimin229/ramaverse-studio/actions/workflows/build-release.yml/badge.svg)](https://github.com/jaimin229/ramaverse-studio/actions/workflows/build-release.yml)
[![Tests](https://img.shields.io/badge/tests-30%2F30%20passing-brightgreen.svg)](RamaverseStudio.Tests)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-black.svg)](https://github.com/jaimin229/ramaverse-studio)
[![Framework](https://img.shields.io/badge/.NET-10.0%20WPF-white.svg)](https://dotnet.microsoft.com/)
[![Languages](https://img.shields.io/badge/languages-English%20%7C%20हिन्दी-blueviolet.svg)](#-worldwide)

---

## 🎬 Overview

**Ramaverse Studio** is a 100% native Windows desktop application crafted in C# .NET 10. Zero web wrappers, zero Electron, zero AI-slop neon effects. Broadcast-grade production capabilities, low-latency audio DSP, real-time voice changing, hardware video encoding, RTMP live streaming, wireless phone camera support, and an in-app Auto-Updater.

---

## ✨ Features

- **🖤 Pure Monochrome Studio Aesthetic**: Precision Black & White interface designed for pro audio/video creators.
- **🖥️ 60 FPS Multi-Layer Compositor**:
  - Multi-Monitor Display capture (GDI `BitBlt`) with cursor toggle.
  - Active Window capture (Win32 `PrintWindow`).
  - Webcams & Capture Cards (`FlashCap`) — **auto-starts with your saved scenes**.
  - Wireless Phone Camera receiver (DroidCam / IP Webcam MJPEG) with auto-reconnect.
  - GDI+ Styled Typography & Vector Text Overlays (live-editable text properties).
  - PNG Image Overlays, Solid Color layers, and Audio Spectrum Visualizer.
  - **True Z-order rendering** — the Up/Down layer buttons actually control draw order.
- **🎚️ Real-Time Audio DSP Signal Flow**:
  - `Mic -> Noise Suppression -> Noise Gate -> 3-Band Parametric EQ -> Dynamic Compressor -> Brickwall Limiter -> Voice Changer -> Master Output`
  - **WASAPI capture with any channel count** (mono mics fully supported) + automatic resampling from any device rate to the 48 kHz master.
  - **Voice Changer Presets**: *Original, Deep Voice, High Voice, Man, Woman, Boy, Girl, Robot (Ring Modulator), Alien, Radio, Megaphone (Overdrive)*.
  - Segmented High-Resolution monochrome dBFS audio VU meters with peak-hold.
  - **Sidechain Auto-Ducking**: game/desktop audio smoothly dips when you speak.
  - Streamer Soundboard: Air Horn, Victory Chime, Level Up, Laser, Buzzer, Applause + custom audio files (correctly decoded, any format NAudio supports).
- **🔴 Hardware Recording & Live Streaming**:
  - Hardware accelerated encoders: NVIDIA NVENC (`h264_nvenc`, `hevc_nvenc`), AMD AMF (`h264_amf`), Intel QuickSync (`h264_qsv`), CPU x264/x265/SVT-AV1 — **with encoder-correct presets** (NVENC no longer receives invalid x264 flags).
  - Real-time RTMP/RTMPS push to YouTube Live, Twitch, Kick, or custom servers.
  - **Dual-Stream engine**: simultaneous 16:9 landscape + 9:16 vertical (TikTok/Reels/Shorts) with CenterCrop or LetterboxPad.
  - Live failure diagnostics: FFmpeg stderr is captured and surfaced with actionable messages (missing FFmpeg, disk full, bad stream key).
- **💬 Unified Live Chat**:
  - **Real Twitch IRC integration** — enter your channel name, read live chat anonymously, no login needed. MOD/SUB badges, auto-scroll that respects manual scrolling.
- **⚡ Instant Replay Buffer (30s)**:
  - Pooled reference-counted frames: **bounded memory** (was an unbounded 15 GB allocation before).
  - AI Auto-Clipper: arms from the Audio Mixer header and auto-saves 9:16 vertical clips when your audio peaks with excitement.
- **🔄 In-App Auto-Updater**: GitHub release manifest checks, atomic background extraction and restart. `v`-prefixed versions handled.
- **💾 Project & State Persistence**:
  - Auto-saves scenes, transforms, crop, chroma keys, color grading, and DSP parameters to `%APPDATA%\RamaverseStudio\project_state.json` with **debounced atomic writes** (drag no longer thrashes the disk). Corrupt files are backed up and recovered gracefully.
  - Canvas resolution is now correctly applied on load (was silently ignored).

---

## 🌍 Worldwide (v1.1)

### 🛡️ Crash-Proof Recording (OBS-grade safety)
- **Every recording is captured to crash-safe MKV first**, then instantly stream-copy remuxed (no re-encode) to your chosen format on stop.
- If Windows crashes or power cuts mid-recording, your footage **still survives**.
- On next launch, Ramaverse **automatically finds and recovers** any interrupted recordings and notifies you — no data loss, no manual steps.
- Optional **auto-split** recording via FFmpeg segment muxer (`BuildSegmentedRecordingArgs`) for marathon sessions.

### 🌐 Multi-Language Interface
- **English + हिन्दी (Hindi)** built-in, switchable from Settings → General.
- Framework is data-driven: drop a JSON file into `%APPDATA%\RamaverseStudio\lang\` to add **any language worldwide** — no update needed. Missing keys automatically fall back to English.

### ✨ Studio Power Features
- **Scene cross-fade transitions** — the per-scene transition spec (`Fade (300ms)`) now actually renders: outgoing frame snapshot, alpha-blended over the new scene.
- **Audio monitoring** — hear your processed master mix (exactly what the audience hears) in your headphones, one checkbox in the mixer.
- **Undo / Redo** (`Ctrl+Z` / `Ctrl+Y`) — transform history for drag/resize/rotate mistakes.
- **Drag & drop** — drop PNG/JPG/GIF/WEBP/BMP files anywhere to create instant overlays.
- **Projector window** — borderless fullscreen canvas mirror for a second monitor or projector (Esc to close).
- **Stream health glow** — the preview border turns green/amber/red with connection quality: diagnose drops without reading numbers.
- **Inline rename** — edit any source's name directly in the inspector header.

---

## 🎁 Creator Pack (v1.2)

### 🚀 One-Click FFmpeg Setup
- First launch without FFmpeg now opens a **guided installer** — click Download, wait ~2 minutes, recording and streaming are unlocked. No PATH editing, no zip files, no technical steps. Falls back to a per-user folder when Program Files is not writable.

### 📑 YouTube Chapter Markers
- Every **scene switch**, **AI auto-clip**, and manual **`Ctrl+M`** marker during a session becomes a video chapter.
- On stop, Ramaverse exports a ready-to-paste **YouTube chapters file** (0:00 format, 10s-minimum rules enforced) plus a machine-readable JSON event log for editors.
- Chapters give your videos scrub-bar timestamps — a real discoverability win OBS doesn't ship natively.

### 🔔 Real Twitch Alerts (sub / raid / bits)
- The Twitch IRC integration now parses **USERNOTICE** tags: subscriptions, gifted subs, raids (with viewer counts) and **bits cheers** from PRIVMSG tags.
- Every event lands in the Alerts dock **and** raises a rich `AlertRaised` event with kind/username/amount — ready for on-canvas alert overlays.

### 🎚️ Multi-Track Audio Recording
- Enable it in Settings → Recording: your **microphone and desktop audio are recorded as two separate streams in one file**, with the mic marked as the default track.
- Editors get a clean voice channel untainted by game sound — the feature OBS reserves for its advanced users, here in one checkbox.

### 🏎️ Real-Time Stream Telemetry
- FFmpeg's own progress lines (`bitrate=…kbits/s`, `speed=…x`) are parsed live from stderr. The bitrate display is now **measured reality, not an estimate**, and the encoder speed ratio is exposed for health checks.

### 🖥️ High-DPI Window Capture
- Window capture now derives each window's DPI (`GetDpiForWindow`) so 125%/150% scaled laptops — the worldwide default — capture at the correct resolution instead of cropping.

### ⏱️ Live Timer Sources
- Text sources gain three live modes: **Countdown** (to a target), **Stopwatch**, and **Clock** — rendered every second, perfect for "starting soon" screens.

### 💎 Lossless Archival Recording
- Settings → Recording: **lossless mode** records mathematically lossless (`libx264rgb -qp 0`) for editors and archives — the bitrate knob is bypassed, files are huge, quality is perfect.

### 🪶 Performance & Distribution
- Preview decimation: the canvas renders at full 60 FPS while the preview refreshes at ~30 — halving a ~400 MB/s memory copy at 1080p with no visible difference.
- **Worldwide single-file build**: `dotnet publish -p:PublishWorldwide=true` produces one self-contained ~73 MB `RamaverseStudio.exe` that runs on any Windows 10/11 x64 machine — no .NET install, no installer.

---

## ⌨️ Hotkeys

| Shortcut | Action |
|---|---|
| `Ctrl + R` | Start / Stop Recording |
| `Ctrl + P` | Pause / Resume Recording |
| `Ctrl + L` | Start / Stop Live Streaming |
| `Ctrl + S` | Instant Full-Resolution PNG Snapshot |
| `Ctrl + O` | Open Recordings Folder |
| `Ctrl + Z` | Undo transform change |
| `Ctrl + Y` | Redo transform change |
| `Ctrl + M` | Add manual chapter marker (recording/streaming) |
| `Ctrl + 1..5` | Quick Scene Transitions (cross-faded) |
| `Arrow Keys` | Nudge selected layer by 1px (`Shift` for 10px) |
| `Delete` | Delete selected layer |
| `Ctrl+Shift + R` | Global: Toggle Recording (works in-game) |
| `Ctrl+Shift + L` | Global: Toggle Streaming |
| `Ctrl+Shift + S` | Global: Snapshot |
| `Ctrl+Shift + M` | Global: Mute Mic |
| `Ctrl+Shift + F10` | Global: Save 30s Replay Clip |

---

## 🛠️ Build & Run

### Prerequisites
- Windows 10/11 x64
- .NET 10.0 SDK
- FFmpeg (added to PATH) — required for recording/streaming. The app detects it missing and shows exactly how to install it.

```bash
# Clone the repository
git clone https://github.com/jaimin229/ramaverse-studio.git
cd ramaverse-studio

# Build and run
dotnet build RamaverseStudio/RamaverseStudio.csproj
.\Launch-RamaverseStudio.bat
```

### Verification Suite

```bash
dotnet run --project RamaverseStudio.Tests
```

30 real integration tests cover the DSP chain (EQ/gate/limiter/compressor/voice-changer), the streaming resampler ratio, chroma key with foreground preservation, project persistence + corruption recovery, the semver updater comparator, hardware discovery, frame-pool reference counting, encoder argument correctness (NVENC/AMF-safe), **the crash-proof MKV capture model + stream-copy remux args + segmented recording**, **localization (English, Hindi, and fallback behavior)**, transition spec parsing, **multi-track stream mapping**, **lossless mode args**, **chapter export format (YouTube rules)**, and a **real live FFmpeg recording** that captures to MKV, auto-remuxes to MP4 with muxed audio, and verifies the final file end-to-end.

### Worldwide Single-File Build

```bash
dotnet publish RamaverseStudio -c Release -p:PublishWorldwide=true
# → one ~73 MB self-contained RamaverseStudio.exe (no .NET install needed)
```

---

## 🧪 Fixed in This Release (Stability Overhaul)

- **Video pipeline corruption**: recorder/streamer/preview shared one mutable buffer — frames were overwritten mid-encode. Now every consumer holds a reference-counted pooled frame.
- **Replay buffer memory bomb**: 30s × 60fps raw frames allocated ~15 GB; now bounded to the reusable pool.
- **Cameras never started**: webcam sources were dead code; they now come online automatically from saved scenes.
- **Mic failure on mono devices**: capture assumed stereo; WASAPI path now handles any channel count and resamples any rate.
- **NVENC/AMF recordings failed to start**: encoder-specific presets (`-preset veryfast` is x264-only).
- **Voice changer 2-second echo**: pitch-down grain read-head wrapped to a stale index.
- **Soundboard custom SFX were pure noise**: float audio was decoded as 16-bit PCM.
- **UI stutter while dragging**: every mouse-move serialized the whole project; saves are now debounced.
- **Saved canvas resolution was ignored on launch**; load order corrected.
- **Stream "stats" were fake**; uptime, drop counts, and failure events are now real, with stderr-based diagnostics.
- Phone camera streams no longer die from an aggressive 10-second HTTP timeout; partial JPEG frames skip instead of breaking the stream.

---

## 📄 License
MIT License. Created for the Ramaverse ecosystem.
