import React, { useState } from 'react';

export default function Hero({ onOpenModal }) {
  const [tilt, setTilt] = useState({ x: 0, y: 0 });

  const handleMouseMove = (e) => {
    const card = e.currentTarget;
    const rect = card.getBoundingClientRect();
    const x = e.clientX - rect.left - rect.width / 2;
    const y = e.clientY - rect.top - rect.height / 2;
    const rotateX = -(y / (rect.height / 2)) * 14;
    const rotateY = (x / (rect.width / 2)) * 14;
    setTilt({ x: rotateX, y: rotateY });
  };

  const handleMouseLeave = () => {
    setTilt({ x: 0, y: 0 });
  };

  return (
    <section className="section-padding hero-hardware-boot" style={{ paddingTop: '60px', paddingBottom: '70px', textAlign: 'center' }}>
      <div className="container">
        
        {/* 3D Interactive Emblem */}
        <div className="hero-emblem-container">
          <div
            className="hero-emblem-card"
            onMouseMove={handleMouseMove}
            onMouseLeave={handleMouseLeave}
            style={{
              transform: `perspective(1000px) rotateX(${tilt.x}deg) rotateY(${tilt.y}deg)`
            }}
          >
            <img
              src="/assets/logo.png"
              alt="Ramaverse Obsidian 3D Emblem"
              className="hero-emblem-img"
            />
          </div>
        </div>

        {/* Live Status Pill */}
        <div style={{ marginBottom: '22px' }}>
          <span className="data-badge" style={{ padding: '6px 14px' }}>
            <span className="tally-dot red"></span>
            <span>Version 1.2.0 Beta • Native Windows x64 • Single 73 MB Binary</span>
          </span>
        </div>

        {/* Kinetic Shimmer Headline */}
        <h1 style={{
          fontSize: 'clamp(2.4rem, 1.8rem + 2.8vw, 4.2rem)',
          fontWeight: 800,
          letterSpacing: '-0.035em',
          lineHeight: 1.1,
          maxWidth: '840px',
          margin: '0 auto 22px auto'
        }}>
          As powerful as OBS.<br />
          <span className="text-sheen">A fraction of the weight.</span>
        </h1>

        {/* Subtitle */}
        <p style={{
          fontSize: 'clamp(1.05rem, 0.95rem + 0.4vw, 1.25rem)',
          color: 'var(--text-secondary)',
          lineHeight: 1.6,
          maxWidth: '700px',
          margin: '0 auto 34px auto'
        }}>
          A 100% native Windows broadcasting studio built in C#. Direct GPU surface capture that won't drop in-game frames, an integrated 5-stage audio DSP chain, and crash-resilient recording.
        </p>

        {/* CTA Button Array */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: '14px',
          flexWrap: 'wrap',
          marginBottom: '36px'
        }}>
          <button
            onClick={onOpenModal}
            className="btn btn-luminous-purple"
            style={{ fontSize: '1rem', padding: '12px 28px' }}
          >
            Download Free Beta (v1.2)
          </button>
          
          <a
            href="#demo"
            className="btn btn-glass-secondary"
            style={{ fontSize: '1rem', padding: '12px 26px' }}
          >
            Explore Master Control ↓
          </a>
        </div>

        {/* Telemetry Chips Bar */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: '12px',
          flexWrap: 'wrap',
          fontSize: '0.8rem',
          fontFamily: 'var(--font-mono)',
          color: 'var(--text-muted)'
        }}>
          <span style={{ color: 'var(--accent-electric)' }}>✓ 73 MB Idle RAM</span>
          <span>•</span>
          <span style={{ color: 'var(--accent-electric)' }}>✓ &lt; 0.5s Startup</span>
          <span>•</span>
          <span style={{ color: 'var(--accent-electric)' }}>✓ 48 kHz WASAPI DSP</span>
          <span>•</span>
          <span style={{ color: 'var(--accent-electric)' }}>✓ Direct D3D11 Capture</span>
        </div>

      </div>
    </section>
  );
}
