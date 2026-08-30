using System;
using System.Threading;
using System.Threading.Tasks;

namespace RamaverseStudio.Services
{
    public class AutoClipperEngine
    {
        public bool IsEnabled { get; set; } = false;
        public double ExcitementThresholdDb { get; set; } = -6.0; // -12 to 0 dB
        public double MinimumPeakDurationSec { get; set; } = 1.0;  // Seconds of loud shouting
        public int CooldownSeconds { get; set; } = 45;

        private double _highEnergyDuration = 0;
        private DateTime _lastTriggerTime = DateTime.MinValue;

        public event Action? ClipTriggered;

        public void ProcessAudioLevel(float currentPeakDb, double deltaSeconds)
        {
            if (!IsEnabled) return;

            if (currentPeakDb >= ExcitementThresholdDb)
            {
                _highEnergyDuration += deltaSeconds;
                if (_highEnergyDuration >= MinimumPeakDurationSec)
                {
                    if ((DateTime.Now - _lastTriggerTime).TotalSeconds >= CooldownSeconds)
                    {
                        _lastTriggerTime = DateTime.Now;
                        _highEnergyDuration = 0;
                        ClipTriggered?.Invoke();
                    }
                }
            }
            else
            {
                _highEnergyDuration = Math.Max(0, _highEnergyDuration - deltaSeconds * 1.5);
            }
        }
    }
}
