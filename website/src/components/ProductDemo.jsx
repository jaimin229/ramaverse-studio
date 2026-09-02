import React, { useState, useRef, useEffect } from 'react';

const NUM_BARS = 36;
const BAR_GAP = 3;

// Procedural Web Audio Sound FX Generator (Zero external audio files required)
function playStudioSfx(type) {
  try {
    const AudioContext = window.AudioContext || window.webkitAudioContext;
    if (!AudioContext) return;
    const ctx = new AudioContext();

    if (type === 'horn') {
      const osc1 = ctx.createOscillator();
      const osc2 = ctx.createOscillator();
      const gain = ctx.createGain();
      osc1.type = 'sawtooth';
      osc2.type = 'sawtooth';
      osc1.frequency.setValueAtTime(466.16, ctx.currentTime); // Bb4
      osc2.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
      gain.gain.setValueAtTime(0.2, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.6);
      osc1.connect(gain);
      osc2.connect(gain);
      gain.connect(ctx.destination);
      osc1.start();
      osc2.start();
      osc1.stop(ctx.currentTime + 0.6);
      osc2.stop(ctx.currentTime + 0.6);
    } else if (type === 'clip') {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(880, ctx.currentTime);
      osc.frequency.exponentialRampToValueAtTime(1760, ctx.currentTime + 0.2);
      gain.gain.setValueAtTime(0.25, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.3);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.3);
    } else if (type === 'mute') {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'square';
      osc.frequency.setValueAtTime(320, ctx.currentTime);
      osc.frequency.exponentialRampToValueAtTime(160, ctx.currentTime + 0.15);
      gain.gain.setValueAtTime(0.15, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.15);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.15);
    } else {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(523.25, ctx.currentTime); // C5
      gain.gain.setValueAtTime(0.15, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.12);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.12);
    }
  } catch (e) {
    // AudioContext blocked or not supported
  }
}

export default function ProductDemo() {
  const [activeScene, setActiveScene] = useState('Game + Facecam');
  const [isRecording, setIsRecording] = useState(true);
  const [isMuted, setIsMuted] = useState(false);
  const [timecode, setTimecode] = useState('00:24:18:42');
  const [toastMessage, setToastMessage] = useState(null);
  const [activePad, setActivePad] = useState(null);

  const [dspStages, setDspStages] = useState([
    { id: 'gate', name: '1. Noise Gate', val: '-42 dBFS Threshold', active: true },
    { id: 'eq', name: '2. 3-Band Parametric EQ', val: 'Low +2.5dB • High +3.2dB', active: true },
    { id: 'comp', name: '3. Dynamic Compressor', val: '3.5:1 Ratio • -18dB', active: true },
    { id: 'limiter', name: '4. Brickwall Limiter', val: 'Ceiling -0.1 dBFS', active: true },
    { id: 'ducking', name: '5. Sidechain Auto-Ducker', val: '-12 dB Game Attenuation', active: true }
  ]);

  const canvasRef = useRef(null);
  const animationRef = useRef(null);

  const showToast = (msg) => {
    setToastMessage(msg);
    setTimeout(() => setToastMessage(null), 2200);
  };

  // Timecode tick simulation
  useEffect(() => {
    let frame = 42;
    let sec = 18;
    let min = 24;
    const timer = setInterval(() => {
      if (!isRecording) return;
      frame += 1;
      if (frame >= 60) {
        frame = 0;
        sec += 1;
        if (sec >= 60) {
          sec = 0;
          min += 1;
        }
      }
      const pad = (n) => String(n).padStart(2, '0');
      setTimecode(`00:${pad(min)}:${pad(sec)}:${pad(frame)}`);
    }, 1000 / 30);
    return () => clearInterval(timer);
  }, [isRecording]);

  // Zero-GC 60 FPS Logarithmic Audio Canvas Renderer
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const bars = new Float32Array(NUM_BARS);
    const peaks = new Float32Array(NUM_BARS);

    let isVisible = true;
    const observer = new IntersectionObserver(([entry]) => {
      isVisible = entry.isIntersecting;
    }, { threshold: 0.1 });
    observer.observe(canvas);

    const render = (time) => {
      if (!isVisible) {
        animationRef.current = requestAnimationFrame(render);
        return;
      }

      const rect = canvas.getBoundingClientRect();
      const W = canvas.width = rect.width;
      const H = canvas.height = rect.height;

      // Deep Obsidian background
      ctx.fillStyle = '#08070E';
      ctx.fillRect(0, 0, W, H);

      // Grid lines
      ctx.strokeStyle = 'rgba(168, 85, 247, 0.1)';
      ctx.lineWidth = 1;
      [0.25, 0.5, 0.75].forEach((ratio) => {
        const y = H * ratio;
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(W, y);
        ctx.stroke();
      });

      const totalGap = BAR_GAP * (NUM_BARS - 1);
      const barW = Math.max(2, (W - totalGap) / NUM_BARS);
      const t = time * 0.003;

      for (let i = 0; i < NUM_BARS; i++) {
        let target = 0;
        if (!isMuted) {
          const w1 = Math.sin(t * 2.8 + i * 0.28) * 0.45 + 0.5;
          const w2 = Math.cos(t * 4.2 - i * 0.2) * 0.35 + 0.35;
          const noise = (Math.sin(i * 92 + t * 11) * 0.5 + 0.5) * 0.25;
          target = (w1 * 0.5 + w2 * 0.35 + noise) * H * 0.88;
          if (i < 5) target *= 1.2; // Low-end punch
        }

        bars[i] = bars[i] * 0.82 + target * 0.18;
        if (bars[i] > peaks[i]) {
          peaks[i] = bars[i];
        } else {
          peaks[i] = Math.max(0, peaks[i] - 0.75);
        }

        const x = i * (barW + BAR_GAP);
        const h = Math.max(2, bars[i]);
        const y = H - h;

        // Radiant Violet & Ultraviolet Gradient fill
        const barGrad = ctx.createLinearGradient(0, y, 0, H);
        if (isMuted) {
          barGrad.addColorStop(0, '#2A2248');
          barGrad.addColorStop(1, '#161326');
        } else if (i >= 31) {
          barGrad.addColorStop(0, '#EF4444');
          barGrad.addColorStop(1, '#991B1B');
        } else if (i >= 24) {
          barGrad.addColorStop(0, '#FBBF24');
          barGrad.addColorStop(1, '#D97706');
        } else {
          barGrad.addColorStop(0, '#C084FC');
          barGrad.addColorStop(1, '#7C3AED');
        }

        ctx.fillStyle = barGrad;
        ctx.fillRect(x, y, barW, h);

        // Glowing Peak pip
        if (!isMuted && peaks[i] > 2) {
          ctx.fillStyle = peaks[i] > H * 0.85 ? '#EF4444' : '#E9D5FF';
          ctx.fillRect(x, H - peaks[i] - 2, barW, 2);
        }
      }

      animationRef.current = requestAnimationFrame(render);
    };

    animationRef.current = requestAnimationFrame(render);

    return () => {
      if (animationRef.current) cancelAnimationFrame(animationRef.current);
      observer.disconnect();
    };
  }, [isMuted]);

  const toggleDsp = (id) => {
    setDspStages(stages =>
      stages.map(s => s.id === id ? { ...s, active: !s.active } : s)
    );
    playStudioSfx('click');
  };

  const handlePadPress = (padId, sfx, label) => {
    setActivePad(padId);
    playStudioSfx(sfx);
    showToast(`Stream Deck: ${label} Triggered`);
    setTimeout(() => setActivePad(null), 180);
  };

  return (
    <section id="demo" className="section-padding">
      <div className="container">
        <p className="section-label">Master Control Surface</p>
        <h2 className="section-title">
          Live Interactive <span className="text-sheen">Broadcast Deck</span>
        </h2>
        <p className="section-lead" style={{ marginBottom: '36px' }}>
          Test the native 5-stage studio DSP rack, trigger soundboard effects, and switch live scene buses in real time.
        </p>

        {/* Chassis Window */}
        <div style={{
          backgroundColor: '#0C0A14',
          border: '1px solid var(--stroke-violet-specular)',
          borderRadius: '16px',
          overflow: 'hidden',
          boxShadow: '0 25px 80px rgba(0, 0, 0, 0.95), 0 0 50px rgba(147, 51, 234, 0.25)',
          position: 'relative'
        }}>
          {/* Toast Notification */}
          {toastMessage && (
            <div style={{
              position: 'absolute',
              top: '60px',
              left: '50%',
              transform: 'translateX(-50%)',
              zIndex: 50,
              background: 'rgba(147, 51, 234, 0.95)',
              color: '#FFF',
              padding: '8px 18px',
              borderRadius: '999px',
              fontSize: '0.82rem',
              fontFamily: 'var(--font-mono)',
              boxShadow: '0 10px 30px rgba(0,0,0,0.8), 0 0 20px rgba(168,85,247,0.6)',
              animation: 'hardware-boot 0.2s ease forwards'
            }}>
              ⚡ {toastMessage}
            </div>
          )}

          {/* Top Control Bar */}
          <div style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '12px 20px',
            borderBottom: '1px solid var(--stroke-hairline)',
            backgroundColor: '#07050B',
            flexWrap: 'wrap',
            gap: '12px'
          }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span className={`tally-dot ${isRecording ? 'red' : 'amber'}`}></span>
                <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.84rem', fontWeight: 700, letterSpacing: '0.5px' }}>
                  {isRecording ? `REC ${timecode} NDF` : 'STANDBY'}
                </span>
              </div>
              <span style={{ color: 'var(--stroke-hairline)' }}>|</span>
              <span style={{ fontFamily: 'var(--font-mono)', fontSize: '0.78rem', color: 'var(--text-secondary)' }}>
                1080p59.94 • NVENC CBR 8000 Kbps • Ref: Genlock Lock
              </span>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <span className="data-badge">
                <span className="tally-dot green"></span>
                <span>LAN Touch Deck :4455 Ready</span>
              </span>
            </div>
          </div>

          {/* Body Grid */}
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
            gap: '1px',
            backgroundColor: 'var(--stroke-hairline)'
          }}>
            {/* Left: Viewport & Audio Spectrum */}
            <div style={{
              backgroundColor: '#07050D',
              padding: '24px',
              display: 'flex',
              flexDirection: 'column',
              minHeight: '400px'
            }}>
              {/* Program Output Monitor */}
              <div style={{
                flex: 1,
                border: '1px solid var(--stroke-subtle)',
                borderRadius: '10px',
                background: 'radial-gradient(ellipse at center, #150F26 0%, #080610 100%)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                flexDirection: 'column',
                position: 'relative',
                padding: '24px',
                overflow: 'hidden',
                boxShadow: 'inset 0 0 40px rgba(0,0,0,0.8)'
              }}>
                {/* Visual Video Content Mock */}
                <div style={{
                  width: '64px',
                  height: '64px',
                  borderRadius: '16px',
                  background: 'rgba(168, 85, 247, 0.15)',
                  border: '1px solid rgba(168, 85, 247, 0.4)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  marginBottom: '12px',
                  color: 'var(--accent-electric)',
                  boxShadow: '0 0 30px rgba(147, 51, 234, 0.3)'
                }}>
                  <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <polygon points="5 3 19 12 5 21 5 3" />
                  </svg>
                </div>

                <div style={{
                  fontFamily: 'var(--font-mono)',
                  fontSize: '0.92rem',
                  fontWeight: 700,
                  color: '#FFF',
                  marginBottom: '4px',
                  textAlign: 'center'
                }}>
                  PROGRAM BUS: {activeScene.toUpperCase()}
                </div>
                <div style={{ fontSize: '0.78rem', color: 'var(--text-secondary)', textAlign: 'center' }}>
                  Direct3D 11 Surface Capture (0 Bytes RAM Copy)
                </div>

                {/* Subtitle Callout */}
                <div style={{
                  position: 'absolute',
                  bottom: '12px',
                  right: '12px',
                  border: '1px solid var(--stroke-subtle)',
                  padding: '5px 12px',
                  borderRadius: '6px',
                  backgroundColor: 'rgba(10, 8, 18, 0.9)',
                  fontFamily: 'var(--font-mono)',
                  fontSize: '0.72rem',
                  color: isMuted ? 'var(--signal-red)' : 'var(--signal-green)'
                }}>
                  MIC: {isMuted ? 'MUTED' : 'GATE ACTIVE (-42dBFS)'}
                </div>
              </div>

              {/* Real-Time Audio Spectrum Canvas */}
              <div style={{ marginTop: '18px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                  <span style={{ fontSize: '0.72rem', fontFamily: 'var(--font-mono)', color: 'var(--text-secondary)' }}>
                    20 Hz — 20 kHz 3-DECADE FFT SPECTRUM
                  </span>
                  <span style={{ fontSize: '0.72rem', fontFamily: 'var(--font-mono)', color: isMuted ? 'var(--signal-red)' : 'var(--accent-electric)' }}>
                    {isMuted ? 'SIGNAL MUTED' : '-14.2 LUFS BROADCAST TARGET'}
                  </span>
                </div>
                <canvas
                  ref={canvasRef}
                  style={{ width: '100%', height: '56px', display: 'block', borderRadius: '6px', border: '1px solid var(--stroke-subtle)' }}
                />
              </div>

              {/* Scene Quick Switcher */}
              <div style={{ display: 'flex', gap: '8px', marginTop: '14px', flexWrap: 'wrap' }}>
                {['Game + Facecam', 'Full Screen Display', 'Just Chatting DVE'].map((s) => (
                  <button
                    key={s}
                    className="btn"
                    onClick={() => {
                      setActiveScene(s);
                      playStudioSfx('click');
                      showToast(`Switched to: ${s}`);
                    }}
                    style={{
                      padding: '7px 14px',
                      fontSize: '0.76rem',
                      borderRadius: '6px',
                      backgroundColor: activeScene === s ? 'rgba(168, 85, 247, 0.25)' : 'rgba(255,255,255,0.03)',
                      borderColor: activeScene === s ? 'var(--accent-violet)' : 'var(--stroke-hairline)',
                      color: activeScene === s ? '#FFF' : 'var(--text-secondary)'
                    }}
                  >
                    {s}
                  </button>
                ))}
              </div>
            </div>

            {/* Right: 5-Stage DSP & Mobile Touch Deck */}
            <div style={{
              backgroundColor: '#0A0814',
              padding: '24px',
              display: 'flex',
              flexDirection: 'column',
              justifyContent: 'space-between'
            }}>
              <div>
                <div style={{ fontSize: '0.8rem', fontWeight: 700, textTransform: 'uppercase', color: 'var(--accent-electric)', marginBottom: '14px', letterSpacing: '0.6px', fontFamily: 'var(--font-mono)' }}>
                  5-Stage Studio Audio DSP Rack
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginBottom: '22px' }}>
                  {dspStages.map((stage) => (
                    <div
                      key={stage.id}
                      onClick={() => toggleDsp(stage.id)}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        padding: '9px 12px',
                        backgroundColor: stage.active ? 'rgba(168, 85, 247, 0.08)' : 'rgba(255,255,255,0.02)',
                        border: `1px solid ${stage.active ? 'rgba(168, 85, 247, 0.3)' : 'transparent'}`,
                        borderRadius: '6px',
                        fontFamily: 'var(--font-mono)',
                        fontSize: '0.76rem',
                        cursor: 'pointer',
                        userSelect: 'none',
                        transition: 'all 0.12s ease'
                      }}
                    >
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span className={`tally-dot ${stage.active ? 'green' : 'amber'}`}></span>
                        <span style={{ color: stage.active ? '#FFF' : 'var(--text-muted)' }}>
                          {stage.name}
                        </span>
                      </div>
                      <span style={{ color: stage.active ? 'var(--accent-electric)' : 'var(--text-muted)' }}>
                        {stage.val}
                      </span>
                    </div>
                  ))}
                </div>
              </div>

              {/* Stream Deck Soundboard & Action Pads */}
              <div>
                <div style={{ fontSize: '0.8rem', fontWeight: 700, textTransform: 'uppercase', color: 'var(--accent-electric)', marginBottom: '12px', letterSpacing: '0.6px', fontFamily: 'var(--font-mono)' }}>
                  Mobile LAN Stream Deck Pads (:4455)
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '8px', marginBottom: '12px' }}>
                  <div
                    className={`studio-pad ${activePad === 'horn' ? 'active' : ''}`}
                    onClick={() => handlePadPress('horn', 'horn', 'Air Horn SFX')}
                  >
                    <span className="pad-key-tag">PAD 1</span>
                    <span style={{ fontSize: '1.1rem' }}>📢</span>
                    <span style={{ fontSize: '0.64rem', fontFamily: 'var(--font-mono)', color: '#FFF' }}>Air Horn</span>
                  </div>

                  <div
                    className={`studio-pad ${activePad === 'clip' ? 'active' : ''}`}
                    onClick={() => handlePadPress('clip', 'clip', '30s Replay Clip Saved')}
                  >
                    <span className="pad-key-tag">PAD 2</span>
                    <span style={{ fontSize: '1.1rem' }}>⚡</span>
                    <span style={{ fontSize: '0.64rem', fontFamily: 'var(--font-mono)', color: '#FFF' }}>30s Clip</span>
                  </div>

                  <div
                    className={`studio-pad ${activePad === 'mute' ? 'active' : ''}`}
                    onClick={() => {
                      setIsMuted(!isMuted);
                      handlePadPress('mute', 'mute', isMuted ? 'Microphone Live' : 'Microphone Muted');
                    }}
                  >
                    <span className="pad-key-tag">PAD 3</span>
                    <span style={{ fontSize: '1.1rem' }}>{isMuted ? '🔇' : '🎙️'}</span>
                    <span style={{ fontSize: '0.64rem', fontFamily: 'var(--font-mono)', color: isMuted ? '#EF4444' : '#FFF' }}>
                      {isMuted ? 'Unmute' : 'Mute'}
                    </span>
                  </div>

                  <div
                    className={`studio-pad ${activePad === 'rec' ? 'active' : ''}`}
                    onClick={() => {
                      setIsRecording(!isRecording);
                      handlePadPress('rec', 'click', isRecording ? 'Recording Stopped' : 'Recording Started');
                    }}
                  >
                    <span className="pad-key-tag">PAD 4</span>
                    <span style={{ fontSize: '1.1rem' }}>{isRecording ? '⏹' : '⏺'}</span>
                    <span style={{ fontSize: '0.64rem', fontFamily: 'var(--font-mono)', color: isRecording ? '#EF4444' : '#34D399' }}>
                      {isRecording ? 'Stop' : 'Rec'}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Caption */}
        <p style={{
          marginTop: '16px',
          fontSize: '0.8rem',
          color: 'var(--text-muted)',
          fontFamily: 'var(--font-mono)',
          textAlign: 'center'
        }}>
          💡 Click any DSP stage or Stream Deck pad above to test live audio synthesis &amp; visual feedback.
        </p>
      </div>
    </section>
  );
}
