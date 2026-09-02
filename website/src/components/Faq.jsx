import React, { useState } from 'react';

const FAQ_DATA = [
  {
    q: 'Is Ramaverse Studio built on Electron or web wrappers?',
    a: 'No. Ramaverse Studio is a 100% native Windows desktop application written in C# .NET 10 with hardware-accelerated WPF graphics. Not a single line of Chromium or JavaScript runs the core application, ensuring cold startup in under 0.5 seconds and ~73 MB idle RAM footprint.'
  },
  {
    q: 'How does crash-resilient recording actually protect my footage?',
    a: 'Standard MP4 recordings store critical file index metadata (the moov atom) at the very end of the file. If power drops or Windows crashes during recording, the MP4 file becomes completely unreadable. Ramaverse streams raw Matroska (MKV) sequential packets directly to disk in real time. If interrupted, your footage is completely playable up to the exact millisecond of the crash. On clean exit, we run an instant stream-copy remux to MP4 without re-encoding.'
  },
  {
    q: 'Do I need to install Virtual Audio Cables or Voicemeeter?',
    a: 'No. Ramaverse Studio includes a built-in 5-stage studio audio DSP rack running at 48 kHz 32-bit float (Noise Gate, 3-Band Parametric EQ, Dynamic Compressor, Brickwall Limiter, and Sidechain Auto-Ducker). You can achieve radio-clean voice audio directly in the app without third-party VSTs or virtual cables.'
  },
  {
    q: 'How does the mobile Stream Deck remote work?',
    a: 'Ramaverse Studio hosts an ultra-fast local HTTP & WebSocket server on your PC at port :4455 (e.g. http://192.168.1.100:4455). Simply open that URL in Safari, Chrome, or Firefox on any smartphone or tablet connected to your home Wi-Fi to get immediate touch controls for scene switching, audio muting, and soundboard effects.'
  },
  {
    q: 'Can I stream simultaneously to Twitch, YouTube, and TikTok?',
    a: 'Yes. Ramaverse features a native dual-canvas GPU compositor matrix. You can push your standard 1080p landscape stream to Twitch/YouTube while simultaneously broadcasting a 9:16 vertical crop to TikTok Live or Instagram Reels from the same session without doubling GPU load.'
  }
];

export default function Faq() {
  const [openIdx, setOpenIdx] = useState(0);

  return (
    <section id="faq" className="section-padding">
      <div className="container" style={{ maxWidth: '820px' }}>
        <p className="section-label">Frequently Asked Questions</p>
        <h2 className="section-title">
          Straightforward Answers for <span className="text-sheen">Streamers</span>
        </h2>
        <p className="section-lead" style={{ marginBottom: '36px' }}>
          Details on system architecture, crash recovery mechanisms, and hardware compatibility.
        </p>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          {FAQ_DATA.map((item, idx) => {
            const isOpen = openIdx === idx;
            return (
              <div
                key={idx}
                style={{
                  backgroundColor: isOpen ? '#0F0B1C' : '#08060F',
                  border: `1px solid ${isOpen ? 'var(--stroke-subtle)' : 'var(--stroke-hairline)'}`,
                  borderRadius: '12px',
                  overflow: 'hidden',
                  transition: 'all 0.2s ease',
                  boxShadow: isOpen ? '0 10px 30px rgba(0,0,0,0.5), 0 0 20px rgba(147, 51, 234, 0.1)' : 'none'
                }}
              >
                <button
                  onClick={() => setOpenIdx(isOpen ? null : idx)}
                  style={{
                    width: '100%',
                    padding: '18px 22px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    background: 'transparent',
                    border: 'none',
                    color: isOpen ? '#FFF' : 'var(--text-primary)',
                    fontFamily: 'var(--font-display)',
                    fontSize: '1rem',
                    fontWeight: 700,
                    textAlign: 'left',
                    cursor: 'pointer',
                    gap: '12px'
                  }}
                >
                  <span>{item.q}</span>
                  <span style={{
                    color: isOpen ? 'var(--accent-electric)' : 'var(--text-muted)',
                    fontFamily: 'var(--font-mono)',
                    fontSize: '1.2rem',
                    transform: isOpen ? 'rotate(45deg)' : 'none',
                    transition: 'transform 0.15s ease'
                  }}>
                    +
                  </span>
                </button>

                {isOpen && (
                  <div style={{
                    padding: '0 22px 18px 22px',
                    fontSize: '0.9rem',
                    color: 'var(--text-secondary)',
                    lineHeight: 1.65,
                    borderTop: '1px solid rgba(255, 255, 255, 0.04)',
                    paddingTop: '14px'
                  }}>
                    {item.a}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}
