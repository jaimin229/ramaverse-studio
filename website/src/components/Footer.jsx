import React from 'react';

export default function Footer() {
  return (
    <footer className="section" style={{ padding: '40px 0', background: 'var(--bg-surface)' }}>
      <div className="container" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
        
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <img
            src="/assets/logo.png"
            alt="Ramaverse Studio"
            style={{ width: '22px', height: '22px', borderRadius: '4px' }}
          />
          <span style={{ fontWeight: 700, fontSize: '0.88rem', color: '#FFF' }}>
            RAMAVERSE STUDIO
          </span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.72rem', color: 'var(--text-dim)' }}>
            v1.3.0 WIN-X64
          </span>
        </div>

        <div style={{ display: 'flex', gap: '24px', fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
          <a href="/downloads/RamaverseStudio-v1.3.0-Setup.exe" download style={{ color: 'var(--accent-bright)' }}>
            Download EXE
          </a>
          <a href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro" target="_blank" rel="noreferrer">
            Gumroad Store
          </a>
          <a href="https://github.com/jaimin229/ramaverse-studio" target="_blank" rel="noreferrer">
            GitHub Repo
          </a>
        </div>

      </div>
    </footer>
  );
}
