import React from 'react';

export default function Architecture() {
  const specs = [
    {
      tag: "VIDEO PIPELINE",
      title: "Direct3D 11 Zero-Copy Surface Capture",
      desc: "Direct GPU texture sharing via Windows Graphics Capture and Desktop Duplication API. Eliminates CPU memory round-trips for zero in-game frame drops during 1080p60 and 4K recording."
    },
    {
      tag: "BROADCAST CONTROL",
      title: "ATEM Staging Deck & Manual T-Bar",
      desc: "Dual Preview (Staged) and Program (Live) monitors. Execute precision cuts, auto-timed crossfades, and manual T-Bar alpha wipes with SMPTE 93% Action Safe and 80% Title Safe alignment overlays."
    },
    {
      tag: "AUDIO DSP",
      title: "5-Stage SIMD Audio Console",
      desc: "Sub-3ms real-time audio filter chain: Adaptive Noise Gate, 3-Band Parametric BiQuad Equalizer, Soft-Knee Compressor, Peak Limiter, and Hardware Voice Pitch Shifter."
    },
    {
      tag: "POST-PRODUCTION",
      title: "Discrete Multi-Track MKV Output",
      desc: "Record separate audio streams simultaneously for Microphone, Game Audio, Discord Voice Chat, and Music tracks directly inside a single container for effortless video editing."
    },
    {
      tag: "SYSTEM RESILIENCE",
      title: "Crash-Resilient Matroska Muxing",
      desc: "Raw video captures to streaming Matroska fragments before finalization. Sudden power cuts or system restarts are automatically recovered without corrupting the file."
    },
    {
      tag: "HARDWARE INTEROP",
      title: "Mobile Touch Deck LAN Remote",
      desc: "Built-in low-latency WebSocket server on port :4455. Connect any iPhone, iPad, or Android phone to switch scenes, toggle audio mutes, and trigger soundboard sound effects."
    }
  ];

  return (
    <section id="architecture" className="section section-border">
      <div className="container">
        <div style={{ textAlign: 'center', marginBottom: '56px' }}>
          <div className="status-pill" style={{ marginBottom: '12px' }}>
            <span>TECHNICAL SPECIFICATIONS</span>
          </div>
          <h2 style={{ fontSize: '2.2rem', fontWeight: 800, letterSpacing: '-0.02em', color: '#FFF' }}>
            Architected for Modern Windows Hardware
          </h2>
        </div>

        <div className="feature-grid">
          {specs.map((item, i) => (
            <div key={i} className="feature-card">
              <span className="feature-tag">{item.tag}</span>
              <h3 className="feature-title">{item.title}</h3>
              <p className="feature-desc">{item.desc}</p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
