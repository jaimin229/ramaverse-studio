import React from 'react';

export default function PricingSection({ onOpenDownload }) {
  return (
    <section id="pricing" className="section">
      <div className="container">
        
        <div style={{ textAlign: 'center', marginBottom: '56px' }}>
          <div style={{
            display: 'inline-block',
            fontFamily: 'var(--font-mono)',
            fontSize: '0.74rem',
            color: 'var(--accent-bright)',
            marginBottom: '10px'
          }}>
            LIFETIME LICENSING
          </div>
          <h2 style={{ fontSize: '2.4rem', fontWeight: 800, letterSpacing: '-0.025em', color: '#FFFFFF' }}>
            Transparent Pricing. Single Executable.
          </h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.95rem', marginTop: '8px' }}>
            No subscriptions. No mandatory cloud accounts. Run 100% locally on your machine.
          </p>
        </div>

        <div className="pricing-deck-grid">
          
          {/* Free Starter Edition */}
          <div className="pricing-box">
            <h3 style={{ fontSize: '1.3rem', fontWeight: 700, color: '#FFF' }}>Starter Edition</h3>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.86rem', marginTop: '4px' }}>
              Standard desktop recording &amp; live streaming engine for single creators.
            </p>
            
            <div className="pricing-cost">
              $0 <span>free forever</span>
            </div>

            <ul className="pricing-items">
              <li className="pricing-item">
                <span className="check-green">✓</span> 1080p 60 FPS Direct3D 11 Surface Capture
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> Single-Track Master Audio Recording
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> 3-Band Parametric Equalizer &amp; Noise Gate
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> Dual-Monitor Detachable Dock Panels
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> RTMP Live Streaming (Twitch, YouTube, Kick)
              </li>
            </ul>

            <a
              href="/downloads/RamaverseStudio-v1.3.0-Setup.exe"
              className="btn-hw btn-hw-secondary"
              style={{ width: '100%', padding: '12px', textAlign: 'center' }}
              download
            >
              Download Setup.exe ($0 Free)
            </a>
          </div>

          {/* Pro Creator Edition */}
          <div className="pricing-box pro-tier">
            <div className="pricing-pro-tag">LIFETIME ACCESS</div>
            
            <h3 style={{ fontSize: '1.3rem', fontWeight: 700, color: '#FFF' }}>Pro Creator</h3>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.86rem', marginTop: '4px' }}>
              Full broadcast engineering suite for esports streamers and video creators.
            </p>
            
            <div className="pricing-cost">
              $49 <span>one-time payment</span>
            </div>

            <ul className="pricing-items">
              <li className="pricing-item">
                <span className="check-green">✓</span> <strong>Everything in Starter Edition</strong>
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> Discrete 4-Track Audio MKV Separation
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> SIMD Transient Click Denoiser &amp; Voice DSP
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> Simultaneous Dual-Canvas (16:9 + 9:16 Shorts)
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> DirectShow Virtual Camera for Discord &amp; Zoom
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> Blackmagic ATEM Staging Deck &amp; T-Bar Fader
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> Mobile Touch Deck LAN Remote Server (:4455)
              </li>
              <li className="pricing-item">
                <span className="check-green">✓</span> Instant Gumroad License Key Delivery
              </li>
            </ul>

            <a
              href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
              target="_blank"
              rel="noreferrer"
              className="btn-hw btn-hw-primary"
              style={{ width: '100%', padding: '12px', textAlign: 'center', fontSize: '0.96rem' }}
            >
              Purchase Pro Lifetime ($49) →
            </a>
          </div>

        </div>
      </div>
    </section>
  );
}
