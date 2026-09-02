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
                CLOSED BETA ENROLLMENT
              </span>
            </div>

            <h3 id="modal-title" style={{ fontSize: '1.4rem', fontWeight: 800, marginBottom: '8px', color: '#FFF' }}>
              Download Ramaverse Studio Beta
            </h3>
            <p style={{ fontSize: '0.88rem', color: 'var(--text-secondary)', marginBottom: '20px', lineHeight: 1.5 }}>
              Enter your email to receive direct download links for the Windows x64 portable executable (v1.2) and join the creator Discord.
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
                Request Download Link →
              </button>
            </form>

            <div style={{ marginTop: '16px', fontSize: '0.72rem', color: 'var(--text-muted)', textAlign: 'center', fontFamily: 'var(--font-mono)' }}>
              🔒 Zero spam. We only send build update logs and critical security notices.
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
              Beta Access Requested!
            </h3>
            <p style={{ fontSize: '0.88rem', color: 'var(--text-secondary)', marginBottom: '24px', lineHeight: 1.5 }}>
              We've dispatched your Windows x64 build link and Discord invitation to <strong style={{ color: '#FFF' }}>{email}</strong>.
            </p>
            <button
              onClick={onClose}
              className="btn btn-glass-secondary"
              style={{ padding: '10px 24px', fontSize: '0.88rem' }}
            >
              Done
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
