import React, { useState } from 'react';
import Navbar from './components/Navbar';
import Hero from './components/Hero';
import Architecture from './components/Architecture';
import AudioRackPreview from './components/AudioRackPreview';
import Pricing from './components/Pricing';
import Footer from './components/Footer';
import DownloadModal from './components/DownloadModal';

export default function App() {
  const [downloadModalOpen, setDownloadModalOpen] = useState(false);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', background: 'var(--bg-void)' }}>
      <Navbar onOpenDownload={() => setDownloadModalOpen(true)} />

      <main style={{ flex: 1 }}>
        <Hero onOpenDownload={() => setDownloadModalOpen(true)} />
        <Architecture />
        <AudioRackPreview />
        <Pricing onOpenDownload={() => setDownloadModalOpen(true)} />
      </main>

      <Footer />

      <DownloadModal
        isOpen={downloadModalOpen}
        onClose={() => setDownloadModalOpen(false)}
      />
    </div>
  );
}
