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
    <div className="modal-backdrop" onClick={onClose} role="dialog" aria-modal="true">
      <div className="modal-content-card" onClick={(e) => e.stopPropagation()}>
        <button className="modal-close-btn" onClick={onClose} aria-label="Close modal">&times;</button>

        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '14px' }}>
          <span style={{ width: '7px', height: '7px', borderRadius: '50%', background: '#10B981' }}></span>
          <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.74rem', color: 'var(--accent-bright)' }}>
            DIRECT WINDOWS EXECUTABLE
          </span>
        </div>

        <h3 style={{ fontSize: '1.4rem', fontWeight: 800, color: '#FFF', marginBottom: '8px' }}>
          Download Ramaverse Studio v1.3.0
        </h3>
        <p style={{ fontSize: '0.88rem', color: 'var(--text-secondary)', lineHeight: 1.5, marginBottom: '22px' }}>
          Single standalone executable (<code className="mono" style={{ color: '#FFF' }}>RamaverseStudio-v1.3.0-Setup.exe</code>). Direct single-click execution with no ZIP extraction needed.
        </p>

        <a
          href="/downloads/RamaverseStudio-v1.3.0-Setup.exe"
          className="btn-hw btn-hw-primary"
          style={{ width: '100%', padding: '14px', fontSize: '0.96rem', marginBottom: '16px', textAlign: 'center' }}
          download
        >
          Download RamaverseStudio-Setup.exe (78.9 MB)
        </a>

        <div style={{ background: '#050308', border: '1px solid var(--border-hairline)', borderRadius: '6px', padding: '12px', marginBottom: '20px' }}>
          <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.7rem', color: 'var(--text-dim)', marginBottom: '4px' }}>
            SYSTEM REQUIREMENTS
          </div>
          <div style={{ fontSize: '0.78rem', color: 'var(--text-secondary)', lineHeight: 1.5 }}>
            • Windows 10 / Windows 11 (64-Bit)<br />
            • DirectX 11 GPU (NVENC, AMF, QSV Hardware Supported)<br />
            • Single File • Zero System Pollution
          </div>
        </div>

        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.8rem' }}>
          <span style={{ color: 'var(--text-dim)' }}>Need Pro Edition?</span>
          <a
            href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
            target="_blank"
            rel="noreferrer"
            style={{ color: 'var(--accent-bright)', fontWeight: 600, textDecoration: 'underline' }}
          >
            Get Pro Lifetime Key ($49) →
          </a>
        </div>
      </div>
    </div>
  );
}
