import React, { useState } from 'react';

export default function SavingsCalculator({ onOpenModal }) {
  const [useStreamDeck, setUseStreamDeck] = useState(true);
  const [useStreamlabsUltra, setUseStreamlabsUltra] = useState(true);
  const [useVoicemeeter, setUseVoicemeeter] = useState(true);
  const [useVstPlugins, setUseVstPlugins] = useState(false);

  // Calculations
  const hardwareSavings = (useStreamDeck ? 150 : 0) + (useVstPlugins ? 99 : 0);
  const annualSubSavings = (useStreamlabsUltra ? 228 : 0);
  const totalFirstYearDollars = hardwareSavings + annualSubSavings;

  const ramSavedMb = 
    (useStreamDeck ? 180 : 0) + 
    (useStreamlabsUltra ? 1400 : 350) + 
    (useVoicemeeter ? 240 : 0) + 
    (useVstPlugins ? 200 : 0) - 73; // Ramaverse footprint

  return (
    <section id="calculator" className="section-padding" style={{ backgroundColor: 'rgba(5, 4, 10, 0.8)' }}>
      <div className="container">
        <p className="section-label">Interactive ROI &amp; Resource Estimator</p>
        <h2 className="section-title">
          How Much Do You Save With <span className="text-sheen">Ramaverse?</span>
        </h2>
        <p className="section-lead" style={{ marginBottom: '40px' }}>
          Calculate the exact system memory, CPU headroom, and subscription costs you eliminate by switching to our native C# studio.
        </p>

        <div style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
          gap: '24px',
          alignItems: 'start'
        }}>
          {/* Left: Interactive Checkbox Matrix */}
          <div className="spotlight-card" style={{ padding: '30px' }}>
            <h3 style={{ fontSize: '1.15rem', fontWeight: 700, marginBottom: '18px', color: '#FFF' }}>
              Your Current Streaming Setup
            </h3>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              {/* Option 1: Hardware Stream Deck */}
              <label style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 16px',
                borderRadius: '8px',
                background: useStreamDeck ? 'rgba(168, 85, 247, 0.1)' : 'rgba(255, 255, 255, 0.02)',
                border: `1px solid ${useStreamDeck ? 'var(--stroke-subtle)' : 'var(--stroke-hairline)'}`,
                cursor: 'pointer',
                userSelect: 'none'
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  <input
                    type="checkbox"
                    checked={useStreamDeck}
                    onChange={(e) => setUseStreamDeck(e.target.checked)}
                    style={{ accentColor: 'var(--accent-purple)', width: '16px', height: '16px', cursor: 'pointer' }}
                  />
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '0.9rem', color: '#FFF' }}>Physical Stream Deck Hardware</div>
                    <div style={{ fontSize: '0.76rem', color: 'var(--text-secondary)' }}>Replaced by Built-in LAN Mobile Remote (:4455)</div>
                  </div>
                </div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.82rem', color: 'var(--accent-electric)' }}>+$150 value</span>
              </label>

              {/* Option 2: Streamlabs Ultra */}
              <label style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 16px',
                borderRadius: '8px',
                background: useStreamlabsUltra ? 'rgba(168, 85, 247, 0.1)' : 'rgba(255, 255, 255, 0.02)',
                border: `1px solid ${useStreamlabsUltra ? 'var(--stroke-subtle)' : 'var(--stroke-hairline)'}`,
                cursor: 'pointer',
                userSelect: 'none'
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  <input
                    type="checkbox"
                    checked={useStreamlabsUltra}
                    onChange={(e) => setUseStreamlabsUltra(e.target.checked)}
                    style={{ accentColor: 'var(--accent-purple)', width: '16px', height: '16px', cursor: 'pointer' }}
                  />
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '0.9rem', color: '#FFF' }}>Streamlabs Ultra / Pro Subscription</div>
                    <div style={{ fontSize: '0.76rem', color: 'var(--text-secondary)' }}>$19/month paywall for multi-stream &amp; themes</div>
                  </div>
                </div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.82rem', color: 'var(--accent-electric)' }}>+$228/yr</span>
              </label>

              {/* Option 3: Voicemeeter Banana */}
              <label style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 16px',
                borderRadius: '8px',
                background: useVoicemeeter ? 'rgba(168, 85, 247, 0.1)' : 'rgba(255, 255, 255, 0.02)',
                border: `1px solid ${useVoicemeeter ? 'var(--stroke-subtle)' : 'var(--stroke-hairline)'}`,
                cursor: 'pointer',
                userSelect: 'none'
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  <input
                    type="checkbox"
                    checked={useVoicemeeter}
                    onChange={(e) => setUseVoicemeeter(e.target.checked)}
                    style={{ accentColor: 'var(--accent-purple)', width: '16px', height: '16px', cursor: 'pointer' }}
                  />
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '0.9rem', color: '#FFF' }}>Voicemeeter &amp; Virtual Audio Cables</div>
                    <div style={{ fontSize: '0.76rem', color: 'var(--text-secondary)' }}>Replaced by 5-Stage Native 48 kHz WASAPI DSP</div>
                  </div>
                </div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.82rem', color: '#34D399' }}>Zero Audio Lag</span>
              </label>

              {/* Option 4: 3rd-Party VST Audio Plugins */}
              <label style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '12px 16px',
                borderRadius: '8px',
                background: useVstPlugins ? 'rgba(168, 85, 247, 0.1)' : 'rgba(255, 255, 255, 0.02)',
                border: `1px solid ${useVstPlugins ? 'var(--stroke-subtle)' : 'var(--stroke-hairline)'}`,
                cursor: 'pointer',
                userSelect: 'none'
              }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                  <input
                    type="checkbox"
                    checked={useVstPlugins}
                    onChange={(e) => setUseVstPlugins(e.target.checked)}
                    style={{ accentColor: 'var(--accent-purple)', width: '16px', height: '16px', cursor: 'pointer' }}
                  />
                  <div>
                    <div style={{ fontWeight: 600, fontSize: '0.9rem', color: '#FFF' }}>External VST Hosts &amp; Plugin Licenses</div>
                    <div style={{ fontSize: '0.76rem', color: 'var(--text-secondary)' }}>Built-in Gate, EQ, Compressor, Limiter &amp; Auto-Duck</div>
                  </div>
                </div>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.82rem', color: 'var(--accent-electric)' }}>+$99 value</span>
              </label>
            </div>
          </div>

          {/* Right: Calculated Yield Card */}
          <div style={{
            background: 'linear-gradient(145deg, #18112A 0%, #0D091A 100%)',
            border: '1px solid var(--stroke-violet-specular)',
            borderRadius: '16px',
            padding: '32px',
            boxShadow: '0 20px 60px rgba(0, 0, 0, 0.9), 0 0 40px rgba(147, 51, 234, 0.25)',
            display: 'flex',
            flexDirection: 'column',
            justifyContent: 'space-between'
          }}>
            <div>
              <span className="data-badge" style={{ marginBottom: '16px' }}>
                <span className="tally-dot green"></span>
                <span>ESTIMATED ANNUAL RESOURCE YIELD</span>
              </span>

              <div style={{ marginBottom: '24px' }}>
                <div style={{ fontSize: '0.84rem', color: 'var(--text-secondary)', marginBottom: '4px' }}>
                  Total Dollar Savings (Year 1)
                </div>
                <div style={{
                  fontSize: 'clamp(2.5rem, 2rem + 1.5vw, 3.4rem)',
                  fontWeight: 800,
                  fontFamily: 'var(--font-mono)',
                  color: '#FFF',
                  lineHeight: 1
                }}>
                  ${totalFirstYearDollars} <span style={{ fontSize: '1rem', color: 'var(--accent-electric)' }}>USD</span>
                </div>
              </div>

              <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(2, 1fr)',
                gap: '14px',
                borderTop: '1px solid rgba(255,255,255,0.08)',
                paddingTop: '18px',
                marginBottom: '26px'
              }}>
                <div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>SYSTEM RAM RECOVERED</div>
                  <div style={{ fontSize: '1.4rem', fontWeight: 700, color: '#34D399', fontFamily: 'var(--font-mono)' }}>
                    +{Math.max(200, ramSavedMb)} MB
                  </div>
                </div>

                <div>
                  <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', fontFamily: 'var(--font-mono)' }}>1% LOW FPS RECOVERED</div>
                  <div style={{ fontSize: '1.4rem', fontWeight: 700, color: '#34D399', fontFamily: 'var(--font-mono)' }}>
                    +15 to +30 FPS
                  </div>
                </div>
              </div>
            </div>

            <button
              onClick={onOpenModal}
              className="btn btn-luminous-purple"
              style={{ width: '100%', padding: '13px', fontSize: '0.96rem' }}
            >
              Get Ramaverse Studio Free →
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
