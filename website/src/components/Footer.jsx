import React from 'react';

export default function Footer({ onOpenModal }) {
  return (
    <footer style={{
      backgroundColor: '#05040A',
      borderTop: '1px solid var(--stroke-hairline)',
      padding: '50px 0 30px 0',
      position: 'relative',
      zIndex: 1
    }}>
      <div className="container">
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '20px',
          marginBottom: '32px'
        }}>
          {/* Left Brand */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <img
              src="/assets/logo.png"
              alt="Ramaverse Emblem"
              style={{ width: '28px', height: '28px', borderRadius: '6px' }}
            />
            <span style={{ fontWeight: 800, fontSize: '0.96rem', letterSpacing: '-0.01em' }}>
              RAMAVERSE STUDIO
            </span>
            <span className="brand-badge">Windows x64 Native</span>
          </div>

          {/* Quick Links */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '24px', fontSize: '0.86rem' }}>
            <a href="#demo" className="nav-link">Control Deck</a>
            <a href="#features" className="nav-link">Features</a>
            <a href="#benchmarks" className="nav-link">Benchmarks</a>
            <a href="#faq" className="nav-link">FAQ</a>
            <button
              onClick={onOpenModal}
              style={{
                background: 'transparent',
                border: 'none',
                color: 'var(--accent-electric)',
                cursor: 'pointer',
                fontFamily: 'inherit',
                fontSize: '0.86rem',
                fontWeight: 600
              }}
            >
              Get Beta Access →
            </button>
          </div>
        </div>

        {/* Bottom Requirements & Copyright */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          flexWrap: 'wrap',
          gap: '14px',
          borderTop: '1px solid rgba(255, 255, 255, 0.04)',
          paddingTop: '20px',
          fontSize: '0.78rem',
          color: 'var(--text-muted)',
          fontFamily: 'var(--font-mono)'
        }}>
          <div>
            Minimum: Windows 10 (1903+) or Windows 11 x64 • DirectX 11 GPU • 4 GB RAM
          </div>
          <div>
            © {new Date().getFullYear()} Ramaverse Studio. All rights reserved.
          </div>
        </div>
      </div>
    </footer>
  );
}
