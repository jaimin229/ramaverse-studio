import React from 'react';

export default function Pricing({ onOpenDownload }) {
  return (
    <section id="pricing" className="section section-border">
      <div className="container">
        <div style={{ textAlign: 'center', marginBottom: '56px' }}>
          <div className="status-pill" style={{ marginBottom: '12px' }}>
            <span>TRANSPARENT LICENSING</span>
          </div>
          <h2 style={{ fontSize: '2.2rem', fontWeight: 800, letterSpacing: '-0.02em', color: '#FFF' }}>
            Predictable Pricing. No Subscriptions Required.
          </h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.95rem', marginTop: '8px' }}>
            Choose the edition that matches your production workflow.
          </p>
        </div>

        <div className="pricing-grid">
          
          {/* Starter Edition */}
          <div className="pricing-card">
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, color: '#FFF' }}>Starter Edition</h3>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.85rem', marginTop: '4px' }}>
              Essential broadcast &amp; recording tools for single-display creators.
            </p>
            
            <div className="pricing-price">
              $0 <span className="pricing-period">free forever</span>
            </div>

            <ul className="pricing-features">
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> 1080p 60 FPS Direct3D 11 Surface Capture
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> Master Stereo Mix Audio Recording
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> 3-Band Parametric Equalizer &amp; Noise Gate
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> Dual-Monitor Detachable Dock Panels
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> RTMP Live Streaming (YouTube, Twitch, Kick)
              </li>
            </ul>

            <button
              onClick={onOpenDownload}
              className="btn btn-secondary"
              style={{ width: '100%', padding: '12px' }}
            >
              Download Free (v1.3.0)
            </button>
          </div>

          {/* Pro Creator Edition */}
          <div className="pricing-card pro">
            <div className="pricing-badge">LIFETIME LICENSE</div>
            
            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, color: '#FFF' }}>Pro Creator</h3>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.85rem', marginTop: '4px' }}>
              Full commercial production engine for esports streamers and studio broadcasters.
            </p>
            
            <div className="pricing-price">
              $49 <span className="pricing-period">one-time payment</span>
            </div>

            <ul className="pricing-features">
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> <strong>Everything in Starter Edition</strong>
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> Discrete Multi-Track Audio MKV Separation
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> SIMD Transient Click Denoiser &amp; Voice DSP
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> Simultaneous Dual-Canvas (16:9 + 9:16 Shorts)
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> Shared Memory Virtual Camera for Discord &amp; Zoom
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> Blackmagic ATEM Staging Deck &amp; T-Bar Fader
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> Mobile Touch Deck LAN Remote Control (:4455)
              </li>
              <li className="pricing-feature-item">
                <span className="pricing-check">✓</span> Hardware Node-Locked Lifetime Key Delivery
              </li>
            </ul>

            <a
              href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
              target="_blank"
              rel="noreferrer"
              className="btn btn-primary"
              style={{ width: '100%', padding: '12px', fontSize: '0.95rem' }}
            >
              Purchase Pro Lifetime ($49) →
            </a>
          </div>

        </div>
      </div>
    </section>
  );
}
