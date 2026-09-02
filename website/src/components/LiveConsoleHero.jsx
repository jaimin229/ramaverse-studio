import React, { useState, useEffect } from 'react';

export default function LiveConsoleHero({ onOpenDownload }) {
  const [activeScene, setActiveScene] = useState(0);
  const [stagedScene, setStagedScene] = useState(1);
  const [tbarProgress, setTbarProgress] = useState(0);
  const [voiceEffect, setVoiceEffect] = useState('Clean');
  const [timecode, setTimecode] = useState('00:14:28:19');
  const [micLevel, setMicLevel] = useState(68);
  const [gameLevel, setGameLevel] = useState(82);

  const scenes = [
    { name: "01. Direct3D 11 Game Capture", res: "1920x1080 • 60 FPS", color: "#1E1438" },
    { name: "02. Dual Camera & Discord HUD", res: "1920x1080 • 60 FPS", color: "#141C2A" },
    { name: "03. Vertical 9:16 Shorts Canvas", res: "1080x1920 • 60 FPS", color: "#22131F" }
  ];

  // Simulated live timecode clock
  useEffect(() => {
    let frames = 19;
    let secs = 28;
    let mins = 14;
    const timer = setInterval(() => {
      frames = (frames + 1) % 60;
      if (frames === 0) {
        secs = (secs + 1) % 60;
        if (secs === 0) mins++;
      }
      const pad = (n) => String(n).padStart(2, '0');
      setTimecode(`00:${pad(mins)}:${pad(secs)}:${pad(frames)}`);
    }, 1000 / 60);
    return () => clearInterval(timer);
  }, []);

  // Simulated audio meter jitter
  useEffect(() => {
    const audioTimer = setInterval(() => {
      setMicLevel(45 + Math.random() * 40);
      setGameLevel(60 + Math.random() * 32);
    }, 90);
    return () => clearInterval(audioTimer);
  }, []);

  const handleCutTransition = () => {
    const prev = activeScene;
    setActiveScene(stagedScene);
    setStagedScene(prev);
    setTbarProgress(0);
  };

  const handleAutoTransition = () => {
    let p = 0;
    const step = setInterval(() => {
      p += 0.15;
      if (p >= 1) {
        clearInterval(step);
        handleCutTransition();
      } else {
        setTbarProgress(p);
      }
    }, 30);
  };

  return (
    <section style={{ paddingTop: '64px', paddingBottom: '72px', textAlign: 'center' }}>
      <div className="container">
        
        {/* Release Status Badge */}
        <div style={{ marginBottom: '20px' }}>
          <div style={{
            display: 'inline-flex',
            alignItems: 'center',
            gap: '8px',
            padding: '4px 14px',
            background: 'var(--bg-chassis)',
            border: '1px solid var(--border-hairline)',
            borderRadius: '999px',
            fontFamily: 'var(--font-mono)',
            fontSize: '0.74rem',
            color: 'var(--text-secondary)'
          }}>
            <span style={{ width: '7px', height: '7px', borderRadius: '50%', background: '#10B981', boxShadow: '0 0 8px #10B981' }}></span>
            <span>PRODUCTION RELEASE v1.3.0 • NATIVE WINDOWS x64</span>
          </div>
        </div>

        {/* Hero Title */}
        <h1 style={{
          fontSize: 'clamp(2.3rem, 1.8rem + 2.6vw, 4.2rem)',
          fontWeight: 800,
          letterSpacing: '-0.035em',
          lineHeight: 1.12,
          maxWidth: '860px',
          margin: '0 auto 20px auto',
          color: '#FFFFFF'
        }}>
          High-Performance Windows Broadcasting &amp; Recording Studio
        </h1>

        {/* Value Proposition */}
        <p style={{
          fontSize: 'clamp(1rem, 0.95rem + 0.3vw, 1.2rem)',
          color: 'var(--text-secondary)',
          lineHeight: 1.6,
          maxWidth: '720px',
          margin: '0 auto 32px auto'
        }}>
          Direct GPU surface capture that never stalls your in-game framerate. Integrated sub-3ms audio DSP console, Blackmagic ATEM broadcast switching, and crash-resilient multi-track recording.
        </p>

        {/* Action Buttons */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: '14px',
          flexWrap: 'wrap',
          marginBottom: '20px'
        }}>
          <button
            onClick={onOpenDownload}
            className="btn-hw btn-hw-primary"
            style={{ fontSize: '0.96rem', padding: '12px 28px' }}
          >
            Download Free (v1.3.0)
          </button>

          <a
            href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
            target="_blank"
            rel="noreferrer"
            className="btn-hw btn-hw-secondary"
            style={{ fontSize: '0.96rem', padding: '12px 28px', borderColor: 'var(--accent-purple)', color: '#FFF' }}
          >
            Get Pro Lifetime ($49) →
          </a>
        </div>

        {/* Binary Spec Monospace Tag */}
        <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.74rem', color: 'var(--text-dim)', marginBottom: '36px' }}>
          75.3 MB Single Executable • Direct3D 11 Zero-Copy • Windows 10 &amp; 11 64-Bit
        </div>

        {/* Real Interactive Broadcast Console Surface */}
        <div className="broadcast-console-box">
          
          {/* Header Bar */}
          <div className="console-header-bar">
            <div className="console-status-items">
              <span className="tally-beacon preview">
                <span className="tally-dot"></span> PREVIEW STAGED
              </span>
              <span className="tally-beacon live">
                <span className="tally-dot"></span> PROGRAM LIVE
              </span>
              <span style={{ color: 'var(--text-secondary)' }}>SMPTE: <strong style={{ color: '#FFF' }}>{timecode}</strong></span>
            </div>

            <div className="console-status-items">
              <span style={{ color: '#10B981' }}>60.0 FPS LOCKED</span>
              <span style={{ color: 'var(--text-secondary)' }}>CPU: <strong style={{ color: '#FFF' }}>0.4%</strong></span>
              <span style={{ color: 'var(--text-secondary)' }}>DROPPED: <strong style={{ color: '#10B981' }}>0 (0.0%)</strong></span>
            </div>
          </div>

          {/* Dual Monitor Stage + ATEM T-Bar */}
          <div className="console-monitors-row">
            
            {/* Preview Monitor */}
            <div className="monitor-frame" style={{ background: scenes[stagedScene].color }}>
              <span className="monitor-top-tag preview">PREVIEW: {scenes[stagedScene].name}</span>
              <div className="safe-areas-overlay"></div>
              <div className="safe-areas-inner"></div>
              <div className="monitor-screen-content">
                <div style={{ textAlign: 'center', zIndex: 5 }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8rem', color: '#F59E0B', fontWeight: 700 }}>
                    STAGED FOR TRANSITION
                  </div>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-dim)', marginTop: '4px' }}>
                    Action Safe 93% • Title Safe 80%
                  </div>
                </div>
              </div>
            </div>

            {/* ATEM T-Bar Switcher */}
            <div className="atem-tbar-deck">
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.66rem', color: 'var(--text-dim)', fontWeight: 700 }}>
                ATEM T-BAR
              </span>
              
              <div className="tbar-track">
                <div
                  className="tbar-handle"
                  style={{ top: `${(1 - tbarProgress) * 98}px` }}
                ></div>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '6px', width: '100%' }}>
                <button className="atem-btn" onClick={handleCutTransition}>CUT</button>
                <button className="atem-btn" onClick={handleAutoTransition} style={{ background: 'var(--accent-purple)', color: '#FFF' }}>AUTO</button>
              </div>
            </div>

            {/* Program Monitor */}
            <div className="monitor-frame" style={{ background: scenes[activeScene].color }}>
              <span className="monitor-top-tag program">LIVE PROGRAM: {scenes[activeScene].name}</span>
              <div className="monitor-screen-content">
                <div style={{ textAlign: 'center', zIndex: 5 }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.85rem', color: '#EF4444', fontWeight: 800 }}>
                    ON AIR • 1080p60 NVENC
                  </div>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', marginTop: '4px' }}>
                    Multi-Track Master Output (MKV)
                  </div>
                </div>
              </div>
            </div>

          </div>

          {/* Lower Control Deck: Scenes, Audio Mixer, Voice DSP */}
          <div className="console-bottom-rack">
            
            {/* Scenes Column */}
            <div className="rack-section" style={{ borderRight: '1px solid var(--border-hairline)' }}>
              <div className="rack-title">Scene Hierarchy</div>
              {scenes.map((s, idx) => (
                <button
                  key={idx}
                  onClick={() => setStagedScene(idx)}
                  className={`scene-item-btn ${stagedScene === idx ? 'active' : ''}`}
                >
                  <span>{s.name}</span>
                  <span style={{ fontSize: '0.68rem', color: stagedScene === idx ? '#C084FC' : 'var(--text-dim)' }}>
                    {stagedScene === idx ? 'STAGED' : ''}
                  </span>
                </button>
              ))}
            </div>

            {/* Audio Mixer Column */}
            <div className="rack-section">
              <div className="rack-title">Real-Time WASAPI Audio Matrix (48 kHz)</div>
              <div className="mixer-channels-grid">
                
                {/* Channel 1: Mic */}
                <div className="mixer-channel-card">
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.68rem', color: 'var(--text-secondary)' }}>MIC VOCAL</span>
                  <div className="vu-meter-strip">
                    <div className="vu-meter-level" style={{ height: `${micLevel}%` }}></div>
                  </div>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.72rem', color: '#FFF' }}>-6.2 dB</span>
                </div>

                {/* Channel 2: Game */}
                <div className="mixer-channel-card">
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.68rem', color: 'var(--text-secondary)' }}>GAME AUDIO</span>
                  <div className="vu-meter-strip">
                    <div className="vu-meter-level" style={{ height: `${gameLevel}%` }}></div>
                  </div>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.72rem', color: '#FFF' }}>0.0 dB</span>
                </div>

                {/* Channel 3: Discord */}
                <div className="mixer-channel-card">
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.68rem', color: 'var(--text-secondary)' }}>VOICE CHAT</span>
                  <div className="vu-meter-strip">
                    <div className="vu-meter-level" style={{ height: '38%' }}></div>
                  </div>
                  <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.72rem', color: '#FFF' }}>-12.0 dB</span>
                </div>

              </div>
            </div>

            {/* Voice DSP Column */}
            <div className="rack-section" style={{ borderLeft: '1px solid var(--border-hairline)' }}>
              <div className="rack-title">Hardware Voice DSP</div>
              <div className="voice-preset-grid">
                {['Clean', 'Deep Radio', 'Megaphone', 'Robot Pitch'].map((v, i) => (
                  <button
                    key={i}
                    onClick={() => setVoiceEffect(v)}
                    className={`voice-btn ${voiceEffect === v ? 'active' : ''}`}
                  >
                    {v}
                  </button>
                ))}
              </div>
              <div style={{ marginTop: '10px', fontSize: '0.68rem', color: 'var(--text-dim)', fontFamily: 'var(--font-mono)' }}>
                DSP Latency: 1.8ms • Zero Phase Shift
              </div>
            </div>

          </div>

        </div>

      </div>
    </section>
  );
}
