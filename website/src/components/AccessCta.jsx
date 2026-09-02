import React from 'react';

export default function AccessCta({ onOpenModal }) {
  return (
    <section className="section-padding" style={{ position: 'relative', overflow: 'hidden' }}>
      <div className="container">
        <div style={{
          background: 'radial-gradient(ellipse at 50% 0%, #241442 0%, #0D091A 70%, #06040C 100%)',
          border: '1px solid var(--stroke-violet-specular)',
          borderRadius: '20px',
          padding: ' clamp(36px, 24px + 3vw, 64px) 24px',
          textAlign: 'center',
          boxShadow: '0 25px 80px rgba(0, 0, 0, 0.9), 0 0 60px rgba(147, 51, 234, 0.25)',
          position: 'relative'
        }}>
          <span className="data-badge" style={{ marginBottom: '20px' }}>
            <span className="tally-dot green"></span>
            <span>Closed Beta In Progress</span>
          </span>

          <h2 style={{
            fontSize: 'clamp(2rem, 1.5rem + 2vw, 3rem)',
            fontWeight: 800,
            letterSpacing: '-0.03em',
            marginBottom: '16px',
            color: '#FFF'
          }}>
            Experience Native Broadcasting Without the Bloat
          </h2>

          <p style={{
            fontSize: '1.05rem',
            color: 'var(--text-secondary)',
            maxWidth: '600px',
            margin: '0 auto 32px auto',
            lineHeight: 1.6
          }}>
            Download Ramaverse Studio Beta (v1.2) for Windows 10/11 x64. Portable single binary with zero installers and zero cloud subscriptions.
          </p>

          <button
            onClick={onOpenModal}
            className="btn btn-luminous-purple"
            style={{ fontSize: '1.05rem', padding: '14px 36px' }}
          >
            Request Beta Access →
          </button>
        </div>
      </div>
    </section>
  );
}
