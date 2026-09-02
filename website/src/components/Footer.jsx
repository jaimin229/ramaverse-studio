import React from 'react';

export default function Footer() {
  return (
    <footer className="section-border" style={{ background: 'var(--bg-surface)', padding: '48px 0', marginTop: '64px' }}>
      <div className="container" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '20px' }}>
        
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <img
            src="/assets/logo.png"
            alt="Ramaverse Studio"
            style={{ width: '24px', height: '24px', borderRadius: '4px' }}
          />
          <span style={{ fontWeight: 700, fontSize: '0.9rem', color: '#FFF' }}>
            RAMAVERSE STUDIO
          </span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.75rem', color: 'var(--text-dim)' }}>
            v1.3.0 PRODUCTION
          </span>
        </div>

        <div style={{ display: 'flex', gap: '24px', fontSize: '0.84rem', color: 'var(--text-secondary)' }}>
          <a href="https://github.com/jaimin229/ramaverse-studio" target="_blank" rel="noreferrer" style={{ transition: 'color 0.15s' }}>
            GitHub Repository
          </a>
          <a href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro" target="_blank" rel="noreferrer" style={{ transition: 'color 0.15s' }}>
            Gumroad Store
          </a>
          <a href="https://raw.githubusercontent.com/jaimin229/ramaverse-studio/main/update_manifest.json" target="_blank" rel="noreferrer" style={{ transition: 'color 0.15s' }}>
            Update Feed
          </a>
        </div>

      </div>
    </footer>
  );
}
