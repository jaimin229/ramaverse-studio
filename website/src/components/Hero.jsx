import React from 'react';

export default function Hero({ onOpenDownload }) {
  return (
    <section className="section" style={{ textAlign: 'center', paddingTop: '80px', paddingBottom: '80px' }}>
      <div className="container">
        
        {/* Status Pill */}
        <div style={{ marginBottom: '24px' }}>
          <div className="status-pill">
            <span className="beacon ready"></span>
            <span>RAMAVERSE STUDIO v1.3.0 • NATIVE WINDOWS x64</span>
          </div>
        </div>

        {/* Main Headline */}
        <h1 style={{
          fontSize: 'clamp(2.4rem, 2rem + 2.5vw, 4rem)',
          fontWeight: 800,
          letterSpacing: '-0.03em',
          lineHeight: 1.15,
          maxWidth: '860px',
          margin: '0 auto 20px auto',
          color: '#FFFFFF'
        }}>
          High-Performance Windows Broadcasting &amp; Recording Engine
        </h1>

        {/* Subtitle */}
        <p style={{
          fontSize: 'clamp(1rem, 0.95rem + 0.3vw, 1.2rem)',
          color: 'var(--text-secondary)',
          lineHeight: 1.6,
          maxWidth: '680px',
          margin: '0 auto 36px auto'
        }}>
          Engineered for streamers and creators. Hardware Direct3D 11 surface capture, integrated SIMD audio DSP rack, ATEM broadcast staging, and crash-resilient multi-track recording.
        </p>

        {/* CTA Array */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          gap: '14px',
          flexWrap: 'wrap',
          marginBottom: '48px'
        }}>
          <button
            onClick={onOpenDownload}
            className="btn btn-primary"
            style={{ padding: '12px 28px', fontSize: '0.95rem' }}
          >
            Download Free (v1.3.0)
          </button>

          <a
            href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
            target="_blank"
            rel="noreferrer"
            className="btn btn-secondary"
            style={{ padding: '12px 28px', fontSize: '0.95rem', borderColor: '#7C3AED', color: '#FFF' }}
          >
            Get Pro Lifetime ($49) →
          </a>
        </div>

        {/* Live Hardware Telemetry Grid */}
        <div className="telemetry-grid">
          <div className="telemetry-card">
            <div className="telemetry-label">Framerate Pipeline</div>
            <div className="telemetry-value">60.0<span className="telemetry-unit">FPS</span></div>
          </div>
          <div className="telemetry-card">
            <div className="telemetry-label">Audio DSP Latency</div>
            <div className="telemetry-value">1.8<span className="telemetry-unit">MS</span></div>
          </div>
          <div className="telemetry-card">
            <div className="telemetry-label">GPU Surface Interop</div>
            <div className="telemetry-value">0-COPY<span className="telemetry-unit">D3D11</span></div>
          </div>
          <div className="telemetry-card">
            <div className="telemetry-label">Frame Drop Tolerance</div>
            <div className="telemetry-value">0<span className="telemetry-unit">DROPPED</span></div>
          </div>
        </div>

      </div>
    </section>
  );
}
