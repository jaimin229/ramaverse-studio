using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using RamaverseStudio.Models;

namespace RamaverseStudio.Services
{
    /// <summary>
    /// Unified Multi-Platform Chat & Stream Alert Aggregator.
    /// Ingests real-time events from Twitch, Kick, and YouTube and dispatches to WPF UI docks.
    /// </summary>
    public class UnifiedChatEngine : IDisposable
    {
        public ObservableCollection<UnifiedChatMessage> DisplayMessages { get; } = new();
        public ObservableCollection<UnifiedAlertEvent> DisplayAlerts { get; } = new();

        private readonly Dispatcher _dispatcher;

        public event Action<UnifiedChatMessage>? MessageReceived;
        public event Action<UnifiedAlertEvent>? AlertTriggered;

        public UnifiedChatEngine(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void PostSystemMessage(string text)
        {
            var msg = new UnifiedChatMessage
            {
                Platform = PlatformType.System,
                SenderName = "SYSTEM",
                SenderColorHex = "#A855F7",
                RawMessage = text,
                Timestamp = DateTime.Now
            };

            EnqueueMessage(msg);
        }

        public void EnqueueMessage(UnifiedChatMessage msg)
        {
            _dispatcher.InvokeAsync(() =>
            {
                DisplayMessages.Add(msg);
                while (DisplayMessages.Count > 250)
                {
                    DisplayMessages.RemoveAt(0);
                }
                MessageReceived?.Invoke(msg);
            });
        }

        public void EnqueueAlert(UnifiedAlertEvent alert)
        {
            _dispatcher.InvokeAsync(() =>
            {
                DisplayAlerts.Insert(0, alert);
                while (DisplayAlerts.Count > 50)
                {
                    DisplayAlerts.RemoveAt(DisplayAlerts.Count - 1);
                }
                AlertTriggered?.Invoke(alert);
            });
        }

        public void Clear()
        {
            _dispatcher.InvokeAsync(() =>
            {
                DisplayMessages.Clear();
                DisplayAlerts.Clear();
            });
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
