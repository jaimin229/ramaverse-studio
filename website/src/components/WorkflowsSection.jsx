import React from 'react';

export default function WorkflowsSection() {
  const workflows = [
    {
      badge: "ESPORTS VIDEO PIPELINE",
      title: "Direct3D 11 Surface Capture. Zero Stalls.",
      desc: "Captured directly at the GPU compositor layer using Windows Graphics Capture (WGC) and Direct3D 11 shared texture handles. Unlike legacy software, your system never copies 4K raw frames through the CPU, keeping in-game framerates completely locked at 240Hz.",
      metricTitle: "Direct3D 11 Performance",
      metrics: [
        { label: "Memory Copy Latency", val: "0-Copy (GPU VRAM)" },
        { label: "Frame Stalls at 240Hz", val: "0 Frames Dropped" },
        { label: "Hardware Encoders", val: "NVENC / AMF / QSV" }
      ]
    },
    {
      badge: "STUDIO AUDIO CAPTURE",
      title: "Discrete 4-Track MKV Separation.",
      desc: "Record your Microphone, Discord voice chat, Game audio, and Music on separate isolated audio tracks in a single Matroska MKV file. Open your footage in Premiere Pro or DaVinci Resolve with every participant already isolated on their own audio track.",
      metricTitle: "Multi-Track Channel Matrix",
      metrics: [
        { label: "Track 01", val: "Microphone (Mono/Stereo)" },
        { label: "Track 02", val: "Discord & VoIP Chat" },
        { label: "Track 03", val: "Game Audio & Sound FX" }
      ]
    },
    {
      badge: "REAL-TIME DSP ENGINE",
      title: "Sub-3ms SIMD Filter Chain & Click Denoiser.",
      desc: "Integrated real-time audio processor operating at 48,000 Hz 32-bit float. Includes adaptive noise gate, 3-band parametric BiQuad EQ, soft-knee compressor, brickwall peak limiter, and voice pitch DSP with zero audible delay.",
      metricTitle: "DSP Audio Benchmarks",
      metrics: [
        { label: "Processing Latency", val: "1.8 ms (Sub-3ms)" },
        { label: "Audio Precision", val: "32-Bit IEEE Float" },
        { label: "Click Suppression", val: "Hardware Transient Clamping" }
      ]
    },
    {
      badge: "BROADCAST CONTROL",
      title: "ATEM Switcher Deck & Mobile Touch Deck.",
      desc: "Studio mode provides dual Preview staging and Program live monitors with a manual T-Bar fader, SMPTE broadcast safe areas (93% Action / 80% Title), and an embedded WebSocket server on port :4455 that turns your smartphone into a remote macro deck.",
      metricTitle: "Production Control Specs",
      metrics: [
        { label: "Staging Deck", val: "ATEM T-Bar + Cut/Auto" },
        { label: "Alignment Guides", val: "SMPTE EBU 93% / 80%" },
        { label: "Remote Port", val: "WebSocket LAN :4455" }
      ]
    }
  ];

  return (
    <section id="workflows" className="section">
      <div className="container">
        
        <div style={{ textAlign: 'center', marginBottom: '64px' }}>
          <div style={{
            display: 'inline-block',
            fontFamily: 'var(--font-mono)',
            fontSize: '0.74rem',
            color: 'var(--accent-bright)',
            marginBottom: '10px'
          }}>
            CORE PRODUCTION WORKFLOWS
          </div>
          <h2 style={{ fontSize: '2.4rem', fontWeight: 800, letterSpacing: '-0.025em', color: '#FFFFFF' }}>
            Engineered For Serious Broadcasters
          </h2>
        </div>

        <div>
          {workflows.map((wf, idx) => (
            <div key={idx} className="workflow-row">
              <div className="workflow-content">
                <span className="workflow-tag">{wf.badge}</span>
                <h3 className="workflow-title">{wf.title}</h3>
                <p className="workflow-desc">{wf.desc}</p>
              </div>

              <div className="workflow-preview-card">
                <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.76rem', color: 'var(--accent-bright)', fontWeight: 700, marginBottom: '14px', borderBottom: '1px solid var(--border-hairline)', paddingBottom: '8px' }}>
                  {wf.metricTitle}
                </div>
                {wf.metrics.map((m, mi) => (
                  <div key={mi} style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 0', borderBottom: mi < wf.metrics.length - 1 ? '1px solid var(--border-hairline)' : 'none' }}>
                    <span style={{ fontSize: '0.84rem', color: 'var(--text-secondary)' }}>{m.label}</span>
                    <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.84rem', color: '#FFFFFF', fontWeight: 600 }}>{m.val}</span>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>

      </div>
    </section>
  );
}
