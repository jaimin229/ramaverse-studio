import React, { useState, useEffect } from 'react';
import Navbar from './components/Navbar';
import Hero from './components/Hero';
import ProblemFraming from './components/ProblemFraming';
import ProductDemo from './components/ProductDemo';
import Features from './components/Features';
import Benchmarks from './components/Benchmarks';
import SavingsCalculator from './components/SavingsCalculator';
import SocialProof from './components/SocialProof';
import AccessCta from './components/AccessCta';
import Faq from './components/Faq';
import Footer from './components/Footer';
import AccessModal from './components/AccessModal';

export default function App() {
  const [modalOpen, setModalOpen] = useState(false);

  // Global mousemove tracker for radial spotlight border effects
  useEffect(() => {
    const handleMouseMove = (e) => {
      const cards = document.querySelectorAll('.spotlight-card');
      cards.forEach((card) => {
        const rect = card.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;
        card.style.setProperty('--mouse-x', `${x}px`);
        card.style.setProperty('--mouse-y', `${y}px`);
      });
    };

    window.addEventListener('mousemove', handleMouseMove);
    return () => window.removeEventListener('mousemove', handleMouseMove);
  }, []);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', position: 'relative' }}>
      {/* Ambient Lighting Background */}
      <div className="ambient-lighting">
        <div className="ambient-flare-top"></div>
        <div className="ambient-flare-bottom"></div>
        <div className="ambient-blueprint-grid"></div>
      </div>

      <Navbar onOpenModal={() => setModalOpen(true)} />

      <main style={{ position: 'relative', zIndex: 1, flex: 1 }}>
        <Hero onOpenModal={() => setModalOpen(true)} />
        <ProblemFraming />
        <ProductDemo />
        <Features />
        <Benchmarks />
        <SavingsCalculator onOpenModal={() => setModalOpen(true)} />
        <SocialProof />
        <AccessCta onOpenModal={() => setModalOpen(true)} />
        <Faq />
      </main>

      <Footer onOpenModal={() => setModalOpen(true)} />

      <AccessModal
        isOpen={modalOpen}
        onClose={() => setModalOpen(false)}
      />
    </div>
  );
}
