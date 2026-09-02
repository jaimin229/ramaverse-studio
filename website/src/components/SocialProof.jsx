import React from 'react';

export default function SocialProof() {
  return (
    <section className="section-padding">
      <div className="container">
        <p className="section-label">Development Status</p>
        <h2 className="section-title">
          Open Development &amp; <span className="text-sheen">Creator Beta</span>
        </h2>
        <p className="section-lead" style={{ marginBottom: '36px' }}>
          Ramaverse Studio is currently in active closed beta with selected creators and esports broadcasters.
        </p>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))',
          gap: '20px'
        }}>
          {/* Status 1 */}
          <div className="spotlight-card">
            <div>
              <span className="data-badge" style={{ marginBottom: '14px' }}>
                <span className="tally-dot green"></span>
                <span>Active Cohort: 85+ Broadcasters</span>
              </span>
              <h3 style={{ fontSize: '1.1rem', fontWeight: 700, marginBottom: '8px', color: '#FFF' }}>
                Real-World Tournament Testing
              </h3>
              <p style={{ fontSize: '0.86rem', color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                Tested across competitive sessions in Valorant, CS2, and Apex Legends to measure frame pacing and audio sync under sustained GPU load.
              </p>
            </div>
          </div>

          {/* Status 2 */}
          <div className="spotlight-card">
            <div>
              <span className="data-badge" style={{ marginBottom: '14px' }}>
                <span className="tally-dot green"></span>
                <span>Weekly Update Cycle</span>
              </span>
              <h3 style={{ fontSize: '1.1rem', fontWeight: 700, marginBottom: '8px', color: '#FFF' }}>
                Direct Creator Feedback Loop
              </h3>
              <p style={{ fontSize: '0.86rem', color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                New builds, encoder optimizations, and audio filters are deployed weekly based on feedback from our Discord beta community.
              </p>
            </div>
          </div>

          {/* Status 3 */}
          <div className="spotlight-card">
            <div>
              <span className="data-badge" style={{ marginBottom: '14px' }}>
                <span className="tally-dot amber"></span>
                <span>In Development: Plugin SDK</span>
              </span>
              <h3 style={{ fontSize: '1.1rem', fontWeight: 700, marginBottom: '8px', color: '#FFF' }}>
                C# Native Extensibility
              </h3>
              <p style={{ fontSize: '0.86rem', color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                Upcoming native plugin architecture allowing custom video filters, transit shaders, and hardware surface integrations without JavaScript overhead.
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
