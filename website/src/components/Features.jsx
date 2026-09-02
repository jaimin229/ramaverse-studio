import React from 'react';

const FEATURE_LIST = [
  {
    icon: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <rect x="2" y="3" width="20" height="14" rx="2" />
        <line x1="8" y1="21" x2="16" y2="21" />
        <line x1="12" y1="17" x2="12" y2="21" />
      </svg>
    ),
    title: 'Solid 60 FPS Capture in Heavy Gaming',
    benefit: 'Your broadcast framerate stays locked even when your GPU is pushed to 99% in demanding DirectX 12 games like Valorant or CS2.',
    techTitle: 'Direct3D 11 Surface Sharing Architecture',
    techDetail: 'Binds directly to Windows DWM and Direct3D 11 shared texture handles. Video frames stay on the GPU throughout capture and encode without copying raw pixels through system RAM.'
  },
  {
    icon: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
        <polyline points="17 21 17 13 7 13 7 21" />
        <polyline points="7 3 7 8 15 8" />
      </svg>
    ),
    title: 'Crash-Resilient Sequential Recording',
    benefit: 'If your PC crashes or power drops 3 hours into a marathon session, your footage survives intact up to the millisecond of failure.',
    techTitle: 'Matroska Sequential Packet Flushing',
    techDetail: 'Streams raw MKV packets straight to disk in real time. Upon clean exit, runs an instant zero-reencode stream-copy remux to MP4 in under 2 seconds.'
  },
  {
    icon: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <path d="M12 1a3 3 0 0 0-3 3v8a3 3 0 0 0 6 0V4a3 3 0 0 0-3-3z" />
        <path d="M19 10v2a7 7 0 0 1-14 0v-2" />
        <line x1="12" y1="19" x2="12" y2="23" />
        <line x1="8" y1="23" x2="16" y2="23" />
      </svg>
    ),
    title: '5-Stage Studio Audio Rack Built In',
    benefit: 'Gate out loud mechanical keyboard clicks, level out scream peaks, and duck game volume automatically when speaking—zero third-party VSTs needed.',
    techTitle: '48 kHz 32-Bit Float WASAPI DSP Engine',
    techDetail: 'Integrated audio chain featuring Noise Gate, 3-Band Parametric EQ, Dynamic Compressor, Brickwall Limiter, and Sidechain Auto-Ducker.'
  },
  {
    icon: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <rect x="5" y="2" width="14" height="20" rx="2" />
        <line x1="12" y1="18" x2="12.01" y2="18" />
      </svg>
    ),
    title: 'Turn Any Phone Into a Wireless Stream Deck',
    benefit: 'Switch scenes, fire soundboard effects, and mute your microphone from any iPhone or Android browser over home Wi-Fi for $0.',
    techTitle: 'Embedded Local WebSocket Server (:4455)',
    techDetail: 'Runs a high-speed local HTTP/WS server on port 4455. Connects directly over local Wi-Fi with zero app downloads and zero cloud accounts.'
  },
  {
    icon: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <rect x="2" y="2" width="20" height="8" rx="2" />
        <rect x="2" y="14" width="20" height="8" rx="2" />
        <line x1="6" y1="6" x2="6.01" y2="6" />
        <line x1="6" y1="18" x2="6.01" y2="18" />
      </svg>
    ),
    title: 'Single-File Portable Studio Package',
    benefit: 'Backup and export your entire broadcast setup—scenes, webcams, audio filters, and overlays—into a single portable archive.',
    techTitle: '.rama Verified Binary Container',
    techDetail: 'Bundles scene JSON configurations, font bindings, filter states, and local image assets into a single verified backup container.'
  },
  {
    icon: (
      <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
        <rect x="2" y="4" width="12" height="16" rx="2" />
        <rect x="16" y="8" width="6" height="12" rx="1" />
      </svg>
    ),
    title: 'Simultaneous 16:9 + 9:16 Dual Broadcast',
    benefit: 'Stream full 1080p landscape to Twitch/YouTube while pushing a vertical feed to TikTok Live from the exact same session.',
    techTitle: 'Dual-Canvas GPU Compositor Matrix',
    techDetail: 'Generates coordinated 16:9 landscape and 9:16 vertical video streams with hardware-assisted CenterCrop and shared encoder passes.'
  }
];

export default function Features() {
  return (
    <section id="features" className="section-padding">
      <div className="container">
        <p className="section-label">Core Capabilities</p>
        <h2 className="section-title">
          Engineered for <span className="text-sheen">Creators &amp; Broadcasters</span>
        </h2>
        <p className="section-lead" style={{ marginBottom: '48px' }}>
          Every feature is built directly in native C# to deliver broadcast reliability without GPU performance penalties.
        </p>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
          gap: '24px'
        }}>
          {FEATURE_LIST.map((feat, i) => (
            <div key={i} className="spotlight-card">
              <div>
                <div className="bento-icon-box">
                  {feat.icon}
                </div>
                <h3 style={{ fontSize: '1.15rem', fontWeight: 700, marginBottom: '10px', color: '#FFF' }}>
                  {feat.title}
                </h3>
                <p style={{ fontSize: '0.88rem', color: 'var(--text-secondary)', lineHeight: 1.6 }}>
                  {feat.benefit}
                </p>
              </div>

              {/* Technical Disclosure */}
              <details className="tech-disclosure">
                <summary>
                  <span>{feat.techTitle}</span>
                  <span style={{ fontSize: '0.75rem', fontFamily: 'var(--font-mono)' }}>[Expand +]</span>
                </summary>
                <div className="tech-disclosure-body">
                  {feat.techDetail}
                </div>
              </details>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
