import React, { useState, useEffect } from 'react';

export default function AudioRackPreview() {
  const [lowGain, setLowGain] = useState(3);
  const [midGain, setMidGain] = useState(-2);
  const [highGain, setHighGain] = useState(4);
  const [gateThreshold, setGateThreshold] = useState(-42);
  const [meterLevel, setMeterLevel] = useState(65);

  // Simulated live audio VU meter animation
  useEffect(() => {
    const interval = setInterval(() => {
      const base = 50 + (lowGain * 2) + (highGain * 1.5);
      const jitter = Math.random() * 25;
      setMeterLevel(Math.min(95, Math.max(20, base + jitter)));
    }, 80);
    return () => clearInterval(interval);
  }, [lowGain, highGain]);

  return (
    <section id="audio-dsp" className="section section-border">
      <div className="container">
        <div style={{ textAlign: 'center', marginBottom: '40px' }}>
          <div className="status-pill" style={{ marginBottom: '12px' }}>
            <span>DSP ENGINE PREVIEW</span>
          </div>
          <h2 style={{ fontSize: '2.2rem', fontWeight: 800, letterSpacing: '-0.02em', color: '#FFF' }}>
            Sub-3ms Real-Time Audio DSP Console
          </h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.95rem', marginTop: '8px' }}>
            Interactive BiQuad parametric filter response with active VU level monitoring.
          </p>
        </div>

        <div className="dsp-console">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid var(--border-subtle)', paddingBottom: '14px' }}>
            <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.8rem', color: 'var(--accent-light)', fontWeight: 600 }}>
              CHANNEL 01 • BROADCAST VOCAL MASTER
            </span>
            <div style={{ display: 'flex', gap: '16px', fontFamily: 'var(--font-mono)', fontSize: '0.75rem', color: 'var(--text-muted)' }}>
              <span>SAMPLE RATE: 48,000 Hz</span>
              <span>BUFFER: 96 SAMPLES</span>
              <span>PRECISION: 32-BIT FLOAT</span>
            </div>
          </div>

          <div className="dsp-sliders">
            {/* Low Shelf */}
            <div className="dsp-slider-col">
              <span className="telemetry-label">Low Shelf (80Hz)</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: '1.1rem', fontWeight: 700, color: '#FFF' }}>
                {lowGain > 0 ? `+${lowGain}` : lowGain} dB
              </span>
              <input
                type="range"
                min="-12"
                max="12"
                value={lowGain}
                onChange={(e) => setLowGain(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#7C3AED', cursor: 'pointer' }}
              />
            </div>

            {/* Mid Band */}
            <div className="dsp-slider-col">
              <span className="telemetry-label">Mid Peak (1.2kHz)</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: '1.1rem', fontWeight: 700, color: '#FFF' }}>
                {midGain > 0 ? `+${midGain}` : midGain} dB
              </span>
              <input
                type="range"
                min="-12"
                max="12"
                value={midGain}
                onChange={(e) => setMidGain(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#7C3AED', cursor: 'pointer' }}
              />
            </div>

            {/* High Shelf */}
            <div className="dsp-slider-col">
              <span className="telemetry-label">High Shelf (10kHz)</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: '1.1rem', fontWeight: 700, color: '#FFF' }}>
                {highGain > 0 ? `+${highGain}` : highGain} dB
              </span>
              <input
                type="range"
                min="-12"
                max="12"
                value={highGain}
                onChange={(e) => setHighGain(Number(e.target.value))}
                style={{ width: '100%', accentColor: '#7C3AED', cursor: 'pointer' }}
              />
            </div>

            {/* Noise Gate & Meter */}
            <div className="dsp-slider-col">
              <span className="telemetry-label">Noise Gate / VU</span>
              <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
                <div className="dsp-meter-bar">
                  <div className="dsp-meter-fill" style={{ height: `${meterLevel}%` }}></div>
                </div>
                <div style={{ textAlign: 'left' }}>
                  <div style={{ fontFamily: 'var(--font-mono)', fontSize: '0.85rem', color: '#FFF', fontWeight: 600 }}>
                    {gateThreshold} dB
                  </div>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>Threshold</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
