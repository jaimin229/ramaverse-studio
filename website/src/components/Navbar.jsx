import React from 'react';

export default function Navbar({ onOpenDownload }) {
  return (
    <header className="navbar">
      <div className="container nav-container" style={{ width: '100%' }}>
        <a href="#" className="nav-brand">
          <img
            src="/assets/logo.png"
            alt="Ramaverse Studio"
            className="nav-logo"
          />
          <span>RAMAVERSE STUDIO</span>
          <span className="nav-version">v1.3.0</span>
        </a>

        <nav>
          <ul className="nav-links">
            <li><a href="#architecture" className="nav-link">Architecture</a></li>
            <li><a href="#audio-dsp" className="nav-link">Audio DSP</a></li>
            <li><a href="#pricing" className="nav-link">Pricing</a></li>
            <li>
              <a
                href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
                target="_blank"
                rel="noreferrer"
                className="nav-link"
                style={{ color: '#C084FC', fontWeight: 600 }}
              >
                Buy Pro ($49)
              </a>
            </li>
          </ul>
        </nav>

        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <button
            onClick={onOpenDownload}
            className="btn btn-primary"
            style={{ padding: '8px 16px', fontSize: '0.82rem' }}
          >
            Download Free
          </button>
        </div>
      </div>
    </header>
  );
}
