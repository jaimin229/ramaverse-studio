import React, { useEffect } from 'react';

export default function DownloadModal({ isOpen, onClose }) {
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && isOpen) onClose();
    };
    if (isOpen) {
      document.body.style.overflow = 'hidden';
      window.addEventListener('keydown', handleKeyDown);
    } else {
      document.body.style.overflow = '';
    }
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div className="modal-overlay" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-box" onClick={(e) => e.stopPropagation()}>
        <button className="modal-close" onClick={onClose} aria-label="Close modal">&times;</button>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
          <span className="beacon ready"></span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.75rem', color: 'var(--accent-light)' }}>
            DIRECT WINDOWS RELEASE
          </span>
        </div>

        <h3 style={{ fontSize: '1.4rem', fontWeight: 800, color: '#FFF', marginBottom: '8px' }}>
          Download Ramaverse Studio v1.3.0
        </h3>
        <p style={{ fontSize: '0.88rem', color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: '24px' }}>
          Single-file standalone Windows 64-bit executable. Double-click to launch immediately without unzipping or installer clutter.
        </p>

        <a
          href="https://github.com/jaimin229/ramaverse-studio/releases/download/v1.3.0/RamaverseStudio-v1.3.0-Setup.exe"
          className="btn btn-primary"
          style={{ width: '100%', padding: '14px', fontSize: '0.95rem', marginBottom: '14px' }}
          download
        >
          Download RamaverseStudio-v1.3.0-Setup.exe (Direct)
        </a>

        <div style={{ background: 'var(--bg-void)', border: '1px solid var(--border-subtle)', borderRadius: '6px', padding: '14px', marginBottom: '20px' }}>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.72rem', color: 'var(--text-dim)', marginBottom: '4px' }}>
            SYSTEM REQUIREMENTS
          </div>
          <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', lineHeight: 1.5 }}>
            • Windows 10 / Windows 11 (64-bit)<br />
            • DirectX 11 Compatible GPU (NVIDIA NVENC, AMD AMF, Intel QSV supported)<br />
            • 4 GB RAM minimum (8 GB recommended)
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>
            Have a Pro License Key?
          </span>
          <a
            href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
            target="_blank"
            rel="noreferrer"
            style={{ fontSize: '0.78rem', color: 'var(--accent-light)', textDecoration: 'underline' }}
          >
            Purchase Pro ($49) →
          </a>
        </div>
      </div>
    </div>
  );
}
