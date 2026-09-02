import React from 'react';

export default function Benchmarks() {
  return (
    <section id="benchmarks" className="section-padding" style={{ backgroundColor: 'rgba(7, 5, 12, 0.7)' }}>
      <div className="container">
        <p className="section-label">Performance Measurements</p>
        <h2 className="section-title">
          Real Benchmarks. <span className="text-sheen">Disclosed Methodology.</span>
        </h2>
        <p className="section-lead" style={{ marginBottom: '36px' }}>
          Tested during live 1080p 60 FPS recording and streaming workloads on Windows 11.
        </p>

        {/* Benchmark Table Container */}
        <div style={{
          backgroundColor: '#0A0812',
          border: '1px solid var(--stroke-subtle)',
          borderRadius: '14px',
          overflowX: 'auto',
          boxShadow: '0 20px 60px rgba(0, 0, 0, 0.8), 0 0 30px rgba(147, 51, 234, 0.15)'
        }}>
          <table style={{
            width: '100%',
            borderCollapse: 'collapse',
            textAlign: 'left',
            fontFamily: 'var(--font-mono)',
            fontSize: '0.84rem',
            minWidth: '600px'
          }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--stroke-subtle)', backgroundColor: '#0F0B1C' }}>
                <th style={{ padding: '16px 20px', color: 'var(--text-secondary)', fontWeight: 600 }}>METRIC</th>
                <th style={{ padding: '16px 20px', color: 'var(--accent-electric)', fontWeight: 700, backgroundColor: 'rgba(168, 85, 247, 0.12)' }}>
                  RAMAVERSE STUDIO (v1.2)
                </th>
                <th style={{ padding: '16px 20px', color: 'var(--text-secondary)', fontWeight: 600 }}>OBS STUDIO 30.X</th>
                <th style={{ padding: '16px 20px', color: 'var(--text-secondary)', fontWeight: 600 }}>STREAMLABS DESKTOP</th>
              </tr>
            </thead>
            <tbody>
              <tr style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}>
                <td style={{ padding: '14px 20px', color: 'var(--text-primary)' }}>Architecture &amp; Shell</td>
                <td style={{ padding: '14px 20px', color: '#FFF', fontWeight: 600, backgroundColor: 'rgba(168, 85, 247, 0.06)' }}>
                  Native C# .NET 10 (Zero Electron)
                </td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>C / C++ Native</td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>Electron + React Wrapper</td>
              </tr>
              <tr style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}>
                <td style={{ padding: '14px 20px', color: 'var(--text-primary)' }}>Idle RAM Footprint</td>
                <td style={{ padding: '14px 20px', color: '#34D399', fontWeight: 600, backgroundColor: 'rgba(168, 85, 247, 0.06)' }}>
                  ~73 MB
                </td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>~280 MB</td>
                <td style={{ padding: '14px 20px', color: '#EF4444' }}>~1,450 MB</td>
              </tr>
              <tr style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}>
                <td style={{ padding: '14px 20px', color: 'var(--text-primary)' }}>Cold Startup Time</td>
                <td style={{ padding: '14px 20px', color: '#34D399', fontWeight: 600, backgroundColor: 'rgba(168, 85, 247, 0.06)' }}>
                  &lt; 0.5 seconds
                </td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>~3.5 seconds</td>
                <td style={{ padding: '14px 20px', color: '#EF4444' }}>~8.0 seconds</td>
              </tr>
              <tr style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}>
                <td style={{ padding: '14px 20px', color: 'var(--text-primary)' }}>Audio DSP Signal Rack</td>
                <td style={{ padding: '14px 20px', color: '#34D399', fontWeight: 600, backgroundColor: 'rgba(168, 85, 247, 0.06)' }}>
                  Built-in 5-Stage 48 kHz WASAPI
                </td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>Requires 3rd-Party VSTs</td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>Basic Filters Only</td>
              </tr>
              <tr style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}>
                <td style={{ padding: '14px 20px', color: 'var(--text-primary)' }}>Mobile Touch Deck</td>
                <td style={{ padding: '14px 20px', color: '#34D399', fontWeight: 600, backgroundColor: 'rgba(168, 85, 247, 0.06)' }}>
                  Built-in LAN (:4455) • $0 Free
                </td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>Requires External Plugins</td>
                <td style={{ padding: '14px 20px', color: '#FBBF24' }}>Paid (Streamlabs Ultra)</td>
              </tr>
              <tr>
                <td style={{ padding: '14px 20px', color: 'var(--text-primary)' }}>Crash Recovery Protection</td>
                <td style={{ padding: '14px 20px', color: '#34D399', fontWeight: 600, backgroundColor: 'rgba(168, 85, 247, 0.06)' }}>
                  Sequential MKV + Instant MP4 Remux
                </td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>Manual Remux Utility</td>
                <td style={{ padding: '14px 20px', color: 'var(--text-secondary)' }}>Manual Remux Utility</td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* Test Rig Disclosure */}
        <div style={{
          marginTop: '18px',
          padding: '12px 18px',
          backgroundColor: '#08060E',
          border: '1px solid var(--stroke-hairline)',
          borderRadius: '8px',
          fontFamily: 'var(--font-mono)',
          fontSize: '0.76rem',
          color: 'var(--text-muted)'
        }}>
          <strong style={{ color: 'var(--text-secondary)' }}>Test Environment:</strong> Intel Core i7-13700K, NVIDIA RTX 4070 (Driver 551.86), 32 GB DDR5 RAM, Windows 11 Pro 23H2. Workload: 1080p 60 FPS NVENC H.264 broadcast with background DirectX 12 game active.
        </div>
      </div>
    </section>
  );
}
