import React, { useState, useEffect, useRef } from 'react';

export default function AccessModal({ isOpen, onClose }) {
  const [email, setEmail] = useState('');
  const [platform, setPlatform] = useState('Twitch');
  const [submitted, setSubmitted] = useState(false);
  const inputRef = useRef(null);

  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden';
      setTimeout(() => inputRef.current?.focus(), 50);
    } else {
      document.body.style.overflow = '';
      setSubmitted(false);
      setEmail('');
    }
  }, [isOpen]);

  // ESC key handler
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.key === 'Escape' && isOpen) {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  const handleSubmit = (e) => {
    e.preventDefault();
    if (!email) return;
    setSubmitted(true);
  };

  return (
    <div
      className="modal-overlay"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-title"
    >
      <div
        className="modal-box"
        onClick={(e) => e.stopPropagation()}
        style={{ animation: 'hardware-boot 0.25s cubic-bezier(0.16, 1, 0.3, 1) forwards' }}
      >
        <button
          className="modal-close"
          onClick={onClose}
          aria-label="Close dialog"
        >
          &times;
        </button>

        {!submitted ? (
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '14px' }}>
              <span className="tally-dot green"></span>
              <span style={{ fontSize: '0.76rem', fontFamily: 'var(--font-mono)', color: 'var(--accent-electric)' }}>
                PRODUCTION RELEASE v1.3.0
              </span>
            </div>

            <h3 id="modal-title" style={{ fontSize: '1.4rem', fontWeight: 800, marginBottom: '8px', color: '#FFF' }}>
              Download Ramaverse Studio v1.3.0
            </h3>
            <p style={{ fontSize: '0.88rem', color: 'var(--text-secondary)', marginBottom: '20px', lineHeight: 1.5 }}>
              Download the standalone Windows x64 package (v1.3.0). Complete with Blackmagic ATEM T-Bar staging, DirectShow virtual camera, and hardware NVENC recording.
            </p>

            <form onSubmit={handleSubmit}>
              <label style={{ display: 'block', fontSize: '0.78rem', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)', marginBottom: '6px' }}>
                CREATOR EMAIL ADDRESS
              </label>
              <input
                ref={inputRef}
                type="email"
                required
                placeholder="creator@example.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="form-input"
              />

              <label style={{ display: 'block', fontSize: '0.78rem', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)', marginBottom: '6px' }}>
                PRIMARY BROADCAST PLATFORM
              </label>
              <select
                value={platform}
                onChange={(e) => setPlatform(e.target.value)}
                className="form-input"
                style={{ cursor: 'pointer' }}
              >
                <option value="Twitch">Twitch</option>
                <option value="YouTube">YouTube Gaming</option>
                <option value="Kick">Kick</option>
                <option value="TikTok">TikTok Live (Vertical)</option>
                <option value="Local">Local Video Recording Only</option>
              </select>

              <button
                type="submit"
                className="btn btn-luminous-purple"
                style={{ width: '100%', padding: '12px', marginTop: '8px', fontSize: '0.95rem' }}
              >
                Download Free Package (Win x64) →
              </button>
            </form>

            <div style={{ marginTop: '16px', fontSize: '0.72rem', color: 'var(--text-muted)', textAlign: 'center', fontFamily: 'var(--font-mono)' }}>
              100% Native Windows • No bloat • Clean portable executable
            </div>
          </div>
        ) : (
          <div style={{ textAlign: 'center', padding: '16px 0' }}>
            <div style={{
              width: '56px',
              height: '56px',
              borderRadius: '50%',
              background: 'rgba(52, 211, 153, 0.15)',
              border: '1px solid rgba(52, 211, 153, 0.4)',
              color: '#34D399',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              fontSize: '1.5rem',
              margin: '0 auto 16px auto'
            }}>
              ✓
            </div>
            <h3 style={{ fontSize: '1.3rem', fontWeight: 800, marginBottom: '8px', color: '#FFF' }}>
              Your Download Is Ready!
            </h3>
            <p style={{ fontSize: '0.88rem', color: 'var(--text-secondary)', marginBottom: '20px', lineHeight: 1.5 }}>
              Click below to download Ramaverse Studio v1.3.0 directly for Windows 64-bit:
            </p>

            <a
              href="https://github.com/jaimin229/ramaverse-studio/releases/download/v1.3.0/RamaverseStudio-v1.3.0-win-x64.zip"
              className="btn btn-luminous-purple"
              style={{ display: 'inline-block', padding: '12px 28px', fontSize: '0.95rem', marginBottom: '12px' }}
              download
            >
              Download RamaverseStudio-v1.3.0.zip
            </a>

            <div style={{ marginTop: '8px' }}>
              <a
                href="https://jaimin229.gumroad.com/l/ramaverse-studio-pro"
                target="_blank"
                rel="noreferrer"
                className="btn btn-glass-secondary"
                style={{ display: 'inline-block', padding: '10px 20px', fontSize: '0.85rem' }}
              >
                Get Pro License Key ($49) →
              </a>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
