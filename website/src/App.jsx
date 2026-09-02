import React, { useState } from 'react';
import Navbar from './components/Navbar';
import LiveConsoleHero from './components/LiveConsoleHero';
import WorkflowsSection from './components/WorkflowsSection';
import PricingSection from './components/PricingSection';
import Footer from './components/Footer';
import DownloadModal from './components/DownloadModal';

export default function App() {
  const [downloadOpen, setDownloadOpen] = useState(false);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', background: 'var(--bg-deep)' }}>
      <Navbar onOpenDownload={() => setDownloadOpen(true)} />

      <main style={{ flex: 1 }}>
        <LiveConsoleHero onOpenDownload={() => setDownloadOpen(true)} />
        <WorkflowsSection />
        <PricingSection onOpenDownload={() => setDownloadOpen(true)} />
      </main>

      <Footer />

      <DownloadModal
        isOpen={downloadOpen}
        onClose={() => setDownloadOpen(false)}
      />
    </div>
  );
}
