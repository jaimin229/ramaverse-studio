# RAMAVERSE STUDIO

> **Record. Stream. Create. Your complete native creator studio in one place.**

[![Build & Release](https://github.com/jaimin229/ramaverse-studio/actions/workflows/build-release.yml/badge.svg)](https://github.com/jaimin229/ramaverse-studio/actions/workflows/build-release.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-black.svg)](https://github.com/jaimin229/ramaverse-studio)
[![Framework](https://img.shields.io/badge/.NET-10.0%20WPF-white.svg)](https://dotnet.microsoft.com/)

---

## 🎬 Overview

**Ramaverse Studio** is a 100% native Windows desktop application crafted in C# .NET 10. Built with zero web wrappers and zero AI-slop neon effects, it offers broadcast-grade production capabilities, low-latency audio DSP, real-time voice changing, hardware video encoding, RTMP live streaming, wireless phone camera support, and an in-app Auto-Updater.

---

## ✨ Features

- **🖤 Pure Monochrome Studio Aesthetic**: Precision Black & White interface designed for pro audio/video creators.
- **🖥️ 60 FPS Multi-Layer Compositor**:
  - Multi-Monitor Display capture (GDI `BitBlt`) with cursor toggle.
  - Active Window capture (Win32 `PrintWindow`).
  - Webcams & Capture Cards (`FlashCap`).
  - Wireless Phone Camera receiver (DroidCam / IP Webcam MJPEG/RTSP stream).
  - GDI+ Styled Typography & Vector Text Overlays.
  - Video and PNG Image Overlays.
- **🎚️ Real-Time Audio DSP Signal Flow**:
  - `Mic -> Noise Suppression -> Noise Gate -> 3-Band Parametric EQ -> Dynamic Compressor -> Brickwall Limiter -> Voice Changer -> Master Output`.
  - **Voice Changer Presets**: *Original, Deep Voice, High Voice, Man, Woman, Boy, Girl, Robot (Ring Modulator), Alien, Radio, Megaphone (Overdrive)*.
  - Segmented High-Resolution monochrome dBFS audio VU meters.
- **🔴 Hardware Recording & Live Streaming**:
  - Hardware accelerated encoders: NVIDIA NVENC (`h264_nvenc`, `hevc_nvenc`), AMD AMF (`h264_amf`), Intel QuickSync (`h264_qsv`), CPU x264/AV1.
  - Real-time RTMP/RTMPS push to YouTube Live, Twitch, Kick, or custom servers.
- **🔄 In-App Auto-Updater**:
  - Checks for remote updates against GitHub release manifests.
  - Automated atomic background extraction and process restart.
- **💾 Project & State Persistence**:
  - Automatically saves all scenes, transforms, color grading (Proc Amp), chroma keys, and DSP parameters to `%APPDATA%\RamaverseStudio\project_state.json`.

---

## ⌨️ Hotkeys

| Shortcut | Action |
|---|---|
| `Ctrl + R` | Start / Stop Recording |
| `Ctrl + P` | Pause / Resume Recording |
| `Ctrl + L` | Start / Stop Live Streaming |
| `Ctrl + S` | Instant Full-Resolution PNG Snapshot |
| `Ctrl + 1..9` | Quick Scene Transitions |
| `Arrow Keys` | Nudge selected layer by 1px (`Shift + Arrows` for 10px) |
| `Delete` | Delete selected layer |

---

## 🛠️ Build & Run

### Prerequisites
- Windows 10/11 x64
- .NET 10.0 SDK
- FFmpeg (added to PATH)

```bash
# Clone the repository
git clone https://github.com/jaimin229/ramaverse-studio.git
cd ramaverse-studio

# Build and run
dotnet build RamaverseStudio/RamaverseStudio.csproj
.\Launch-RamaverseStudio.bat
```

---

## 📄 License
MIT License. Created for the Ramaverse ecosystem.
