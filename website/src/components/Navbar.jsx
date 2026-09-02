import React, { useState } from 'react';

export default function Navbar({ onOpenModal }) {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <header className="site-nav">
      <div className="nav-glass-bar">
        {/* Brand */}
        <a href="#" className="nav-brand">
          <img
            src="/assets/logo.png"
            alt="Ramaverse Studio Emblem"
            className="nav-logo-icon"
          />
          <span>RAMAVERSE STUDIO</span>
          <span className="brand-badge">BETA v1.2</span>
        </a>

        {/* Desktop Links */}
        <nav aria-label="Main Navigation">
          <ul className="nav-links">
            <li><a href="#demo" className="nav-link">Studio Tour</a></li>
            <li><a href="#features" className="nav-link">Features</a></li>
            <li><a href="#benchmarks" className="nav-link">Benchmarks</a></li>
            <li><a href="#faq" className="nav-link">FAQ</a></li>
          </ul>
        </nav>

        {/* Right CTA */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <button
            onClick={onOpenModal}
            className="btn btn-luminous-purple"
            style={{ fontSize: '0.84rem', padding: '8px 18px' }}
          >
            Join the Beta
          </button>

          {/* Mobile Hamburger Button */}
          <button
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            className="mobile-menu-btn"
            aria-label="Toggle navigation menu"
          >
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              {mobileMenuOpen ? (
                <path d="M18 6L6 18M6 6l12 12" />
              ) : (
                <path d="M4 6h16M4 12h16M4 18h16" />
              )}
            </svg>
          </button>
        </div>
      </div>

      {/* Mobile Drawer */}
      {mobileMenuOpen && (
        <div style={{
          marginTop: '10px',
          background: 'rgba(15, 11, 26, 0.96)',
          backdropFilter: 'blur(20px)',
          border: '1px solid var(--stroke-subtle)',
          borderRadius: '16px',
          padding: '20px',
          display: 'flex',
          flexDirection: 'column',
          gap: '14px',
          boxShadow: '0 15px 40px rgba(0,0,0,0.8)'
        }}>
          <a
            href="#demo"
            className="nav-link"
            style={{ fontSize: '1rem', padding: '8px 0' }}
            onClick={() => setMobileMenuOpen(false)}
          >
            Studio Tour
          </a>
          <a
            href="#features"
            className="nav-link"
            style={{ fontSize: '1rem', padding: '8px 0' }}
            onClick={() => setMobileMenuOpen(false)}
          >
            Features
          </a>
          <a
            href="#benchmarks"
            className="nav-link"
            style={{ fontSize: '1rem', padding: '8px 0' }}
            onClick={() => setMobileMenuOpen(false)}
          >
            Benchmarks
          </a>
          <a
            href="#faq"
            className="nav-link"
            style={{ fontSize: '1rem', padding: '8px 0' }}
            onClick={() => setMobileMenuOpen(false)}
          >
            FAQ
          </a>
          <button
            onClick={() => {
              setMobileMenuOpen(false);
              onOpenModal();
            }}
            className="btn btn-luminous-purple"
            style={{ width: '100%', padding: '12px' }}
          >
            Join the Beta
          </button>
        </div>
      )}
    </header>
  );
}
