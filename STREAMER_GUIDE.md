# 🎙️ RAMAVERSE STUDIO — Complete Streamer & Creator Masterclass Guide

Welcome to **Ramaverse Studio** — the ultra-lightweight, high-performance, all-in-one broadcasting and recording software engineered for YouTube creators, Twitch streamers, TikTok Live broadcasters, and esports gamers.

---

## 📑 Table of Contents
1. [🚀 Quick Start (0 to Live in 60s)](#1--quick-start-0-to-live-in-60s)
2. [🎮 Game & Screen Capture Setup](#2--game--screen-capture-setup)
3. [🎤 Broadcast Audio & AI Voice DSP](#3--broadcast-audio--ai-voice-dsp)
4. [🎛️ Studio Mode (Dual Canvas Switcher)](#4--studio-mode-dual-canvas-switcher)
5. [📱 Mobile Touch Deck & Remote Control](#5--mobile-touch-deck--remote-control)
6. [⚡ Instant Replays, Auto-Clipper & YouTube Chapters](#6--instant-replays-auto-clipper--youtube-chapters)
7. [📦 Portable Scene Collections (.rama Archive)](#7--portable-scene-collections-rama-archive)
8. [🌐 Browser Sources & Live Chat Aggregator](#8--browser-sources--live-chat-aggregator)
9. [⌨️ Global Hotkeys Reference Cheat Sheet](#9--global-hotkeys-reference-cheat-sheet)

---

## 1. 🚀 Quick Start (0 to Live in 60s)

### Step 1: Add Sources
1. In the bottom-left **Sources** panel, click **+ Add Source**.
2. Select **🖥️ Screen Capture** or **🎮 Game Capture** to capture your display or active game.
3. Select **📹 Video Capture Device** to turn on your webcam or DSLR capture card.
4. Drag and resize the layer on the preview canvas using the corner interactive handles.

### Step 2: Configure Microphone & Audio
1. Click **⚙️ Settings** in the top toolbar.
2. Select your microphone device (e.g. HyperX, Rode, Elgato, Shure, Realtek).
3. In the right **Properties** inspector, check **Noise Gate** and **Compressor** to immediately eliminate keyboard clicks, fan hum, and room reverb.

### Step 3: Start Broadcasting or Recording
- **Record (Local MP4)**: Click **● RECORD** or press `Ctrl + R`.
- **Stream Live**: Click **📡 GO LIVE** or press `Ctrl + L`.

---

## 2. 🎮 Game & Screen Capture Setup

Ramaverse Studio utilizes **Windows Graphics Capture (WGC)** and **DirectX/DWM hardware memory sharing**, providing 60/120 FPS capture without frame drops in heavy games (Valorant, CS2, GTA V, Fortnite, Apex Legends).

### Canvas Aspect Ratios
Click the format pills in the bottom toolbar to switch resolutions:
- **16:9 Landscape (1920x1080 / 2560x1440 / 3840x2160)**: Standard for YouTube Live, Twitch, and Kick.
- **9:16 Vertical (1080x1920)**: Built-in native support for TikTok Live, YouTube Shorts, and Instagram Reels.
- **4:3 Retro & 1:1 Square**: For vintage gaming and social media posts.

---

## 3. 🎤 Broadcast Audio & AI Voice DSP

Ramaverse Studio features a studio hardware-grade 5-stage DSP processing chain:

| Module | What It Does | Why Streamers Love It |
| :--- | :--- | :--- |
| **Noise Gate** | Mutes audio below threshold | Keeps mechanical keyboard clicks & background fan hum dead silent |
| **3-Band Studio EQ** | Low Shelf, Mid Peak, High Shelf | Boosts warm radio announcer bass and vocal clarity |
| **Dynamic Compressor** | Smooths out vocal dynamics | Whisper quietly or shout in excitement without volume jumps |
| **Peak Limiter** | Clamps audio at -1.0 dB | Zero distortion or digital clipping on clutch hype screams |
| **Auto-Ducking** | Dips desktop music by 12dB | When you speak into your mic, game sound automatically lowers |
| **Voice Changer** | Real-time pitch/formant shifting | Presets: 🤖 Robot, 🗿 Deep Voice, ✨ Anime, 📢 Megaphone, 👹 Demon |
| **Headphone Monitor** | Zero-latency feedback | Hear your real processed voice and soundboard in your headset |

---

## 4. 🎛️ Studio Mode (Dual Canvas Switcher)

Studio Mode lets creators stage, edit, and inspect scenes in private before pushing them live to their audience.

1. Click **🎛️ STUDIO MODE** in the top toolbar.
2. The canvas splits into two side-by-side viewports:
   - **Left (Preview)**: Staging area. Select scenes, move webcams, or adjust text without viewers seeing.
   - **Right (Program)**: Live broadcast feed being streamed/recorded.
3. Click the center **TRANSITION ➔** button to push your staged scene to the live broadcast with a smooth **CrossFade**, **Slide**, or **Wipe** effect!
4. Click **Projector** in the top right to pop out a borderless fullscreen preview on a secondary monitor or TV (Press `ESC` to close).

---

## 5. 📱 Mobile Touch Deck & Remote Control

Turn any smartphone (iPhone, Android) or tablet (iPad, Galaxy Tab) into a physical wireless Stream Deck!

1. Connect your phone to the same Wi-Fi network as your PC.
2. Click **📱 REMOTE** in the top toolbar to view/copy your local URL (e.g. `http://192.168.1.50:4455`).
3. Open the link in Safari or Chrome on your phone.
4. **Touch Controls Available on Your Phone**:
   - 🔘 1-Tap Scene Switching (live scene buttons)
   - ⏺ Toggle Recording & Duration Timer
   - 📡 Toggle Live Streaming
   - 🔇 Mute / Unmute Microphone
   - ⚡ Trigger Instant Replay Clip
   - 📢 Soundboard SFX Pads (Air Horn, Applause, Victory Chime, Laser, Level Up, Buzzer)

---

## 6. ⚡ Instant Replays, Auto-Clipper & YouTube Chapters

### 30-Second Instant Replay Clip
- Press `Ctrl + Shift + F10` or click **⚡ CLIP (30s)** in the top bar.
- The last 30 seconds of high-FPS video is immediately exported as a standalone MP4 file in your recordings folder without interrupting your live stream.

### AI Excitement Auto-Clipper
- Check **AI Auto-Clipper** in the soundboard drawer.
- The engine listens to your microphone energy. When you scream or celebrate a clutch win, it automatically renders a 9:16 vertical replay clip ready for YouTube Shorts and TikTok!

### Automatic YouTube Chapters
- Every scene change and manual marker (`Ctrl + M`) is logged with exact timecodes.
- When recording stops, a `*_chapters.txt` file is generated containing YouTube-ready timestamp descriptions for instant copy-pasting into your video description:
  ```text
  00:00 Intro & Welcome
  02:15 Gameplay Match 1
  14:30 Epic Clutch Replay
  22:45 Outro & Community Chat
  ```

---

## 7. 📦 Portable Scene Collections (.rama Archive)

Easily back up your stream layouts or share complete overlay templates with fellow creators.

- **Export**: Click the **📦** button in the Scenes toolbar. This bundles all scenes, sources, transform coordinates, audio filter settings, and local image/audio assets into a single portable `.rama` file.
- **Import**: Click the **📂** button on any other computer to extract and relink all assets seamlessly.

---

## 8. 🌐 Browser Sources & Live Chat Aggregator

### Chromium Browser Source (WebView2)
- Add **🌐 Browser Source** to overlay animated StreamElements alerts, donation goals, subscriber counters, and HTML5 animations directly on your stream canvas with full alpha transparency.

### Unified Live Chat Aggregator
- Switch to the **💬 CHAT & ALERTS** tab in the right panel.
- Paste your Twitch channel name or YouTube live video link to see all viewer chat messages consolidated in real time with distinct platform badges (`[T]`, `[Y]`, `[K]`).

---

## 9. ⌨️ Global Hotkeys Reference Cheat Sheet

| Shortcut | Action | Description |
| :--- | :--- | :--- |
| `Ctrl + R` | **Toggle Recording** | Start / Stop crash-proof MP4 recording |
| `Ctrl + L` | **Toggle Streaming** | Start / Stop live broadcast |
| `Ctrl + S` | **Studio Snapshot** | Capture instant high-resolution PNG image |
| `Ctrl + Shift + F10` | **Instant Replay Clip** | Export last 30 seconds to standalone MP4 |
| `Ctrl + M` | **Mark Chapter** | Add instant timestamp chapter marker |
| `Ctrl + 1 ... 5` | **Switch Scenes** | Switch directly to Scene 1 through 5 |
| `Ctrl + Z` | **Undo** | Revert last canvas transform / position change |
| `Ctrl + Y` | **Redo** | Re-apply reverted canvas change |
| `Arrow Keys` | **Nudge Source** | Move selected source by 1px (Hold Shift for 10px) |
| `Delete` | **Delete Source** | Remove currently selected layer |
| `F1` | **Open Guide** | Open the interactive in-app tutorial guide |

---

*Made with ❤️ for Creators worldwide by Ramaverse Studio.*
