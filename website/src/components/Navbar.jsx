import React from 'react';

export default function Navbar({ onOpenDownload }) {
  return (
    <header className="nav-header">
      <div className="container nav-wrap">
        <a href="#" className="brand-group">
          <img
            src="/assets/logo.png"
            alt="Ramaverse Studio"
            className="brand-logo"
          />
          <span className="brand-title">RAMAVERSE STUDIO</span>
          <span className="brand-tag">v1.3.0</span>
        </a>

        <nav>
          <ul className="nav-menu">
            <li><a href="#workflows" className="nav-item-link">Workflows</a></li>
            <li><a href="#pricing" className="nav-item-link">Pricing</a></li>
            <li>
              <a
                href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
                target="_blank"
                rel="noreferrer"
                className="nav-item-link"
                style={{ color: 'var(--accent-bright)', fontWeight: 600 }}
              >
                Buy Pro ($49)
              </a>
            </li>
          </ul>
        </nav>

        <div>
          <a
            href="/downloads/RamaverseStudio-v1.3.0-Setup.exe"
            className="btn-hw btn-hw-primary"
            style={{ padding: '8px 16px', fontSize: '0.82rem' }}
            download
          >
            Download .EXE
          </a>
        </div>
      </div>
    </header>
  );
}
