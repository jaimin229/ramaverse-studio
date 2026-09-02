import React from 'react';

export default function ProblemFraming() {
  return (
    <section className="section-padding" style={{ backgroundColor: 'rgba(7, 5, 12, 0.6)' }}>
      <div className="container">
        <p className="section-label">The Problem With Streaming Software</p>
        <h2 className="section-title">The Gap Between Complex and Bloated</h2>
        <p className="section-lead" style={{ marginBottom: '40px' }}>
          Broadcasting tools today force a bad compromise between fragile plugin chains and bloated web wrappers.
        </p>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
          gap: '20px'
        }}>
          {/* Card 1: Traditional OBS */}
          <div className="spotlight-card">
            <div>
              <div className="bento-icon-box" style={{ background: 'rgba(239, 68, 68, 0.1)', borderColor: 'rgba(239, 68, 68, 0.3)', color: '#EF4444' }}>
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="8" x2="12" y2="12" />
                  <line x1="12" y1="16" x2="12.01" y2="16" />
                </svg>
              </div>
              <h3 style={{ fontSize: '1.2rem', fontWeight: 700, marginBottom: '10px', color: '#FFF' }}>
                The Overcomplicated Setup
              </h3>
              <p style={{ fontSize: '0.9rem', color: 'var(--text-secondary)', lineHeight: 1.6, marginBottom: '16px' }}>
                OBS is capable, but routing audio through virtual cables and keeping 5 third-party plugins from desyncing your microphone requires hours of troubleshooting.
              </p>
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.78rem', color: '#EF4444', borderTop: '1px solid rgba(255,255,255,0.06)', paddingTop: '12px' }}>
              ✕ Audio drift &amp; hook stutters
            </div>
          </div>

          {/* Card 2: Chromium Wrappers */}
          <div className="spotlight-card">
            <div>
              <div className="bento-icon-box" style={{ background: 'rgba(251, 191, 36, 0.1)', borderColor: 'rgba(251, 191, 36, 0.3)', color: '#FBBF24' }}>
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M18.36 6.64a9 9 0 1 1-12.73 0" />
                  <line x1="12" y1="2" x2="12" y2="12" />
                </svg>
              </div>
              <h3 style={{ fontSize: '1.2rem', fontWeight: 700, marginBottom: '10px', color: '#FFF' }}>
                The Chromium Memory Hog
              </h3>
              <p style={{ fontSize: '0.9rem', color: 'var(--text-secondary)', lineHeight: 1.6, marginBottom: '16px' }}>
                "Beginner" streaming suites are essentially web browsers in disguise. They chew up 1.5 GB+ of RAM, inject background telemetry, and lock basic features behind paywalls.
              </p>
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.78rem', color: '#FBBF24', borderTop: '1px solid rgba(255,255,255,0.06)', paddingTop: '12px' }}>
              ✕ 1.5 GB+ RAM &amp; monthly fees
            </div>
          </div>

          {/* Card 3: Ramaverse Studio */}
          <div className="spotlight-card" style={{ borderColor: 'var(--stroke-subtle)', background: 'linear-gradient(180deg, #130E24 0%, #090712 100%)' }}>
            <div>
              <div className="bento-icon-box">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M13 2L3 14h9l-1 8 10-12h-9l1-8z" />
                </svg>
              </div>
              <h3 style={{ fontSize: '1.2rem', fontWeight: 700, marginBottom: '10px', color: '#FFF' }}>
                Ramaverse Native Studio
              </h3>
              <p style={{ fontSize: '0.9rem', color: 'var(--text-secondary)', lineHeight: 1.6, marginBottom: '16px' }}>
                A clean, native desktop application in C# .NET 10. Direct GPU texture sharing, built-in 5-stage studio audio DSP, and crash-resilient recording that never loses a file.
              </p>
            </div>
            <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.78rem', color: 'var(--accent-electric)', borderTop: '1px solid rgba(168,85,247,0.2)', paddingTop: '12px' }}>
              ✓ 73 MB RAM • 0% Idle CPU • 100% Free
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
