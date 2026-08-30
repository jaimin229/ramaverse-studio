using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace RamaverseStudio.Services
{
    public enum ChatPlatform
    {
        YouTube,
        Twitch,
        Kick,
        System
    }

    public class ChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Sender { get; set; } = "Viewer";
        public string Message { get; set; } = "";
        public string Timestamp { get; set; } = DateTime.Now.ToString("HH:mm");
        public ChatPlatform Platform { get; set; } = ChatPlatform.YouTube;
        public bool IsModerator { get; set; } = false;
        public bool IsSubscriber { get; set; } = false;
    }

    public class StreamAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "New Subscriber!";
        public string Details { get; set; } = "Viewer subscribed to channel";
        public string Amount { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ChatAggregatorService
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();
        public ObservableCollection<StreamAlert> Alerts { get; } = new ObservableCollection<StreamAlert>();

        private readonly Dispatcher _dispatcher;

        public ChatAggregatorService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;

            // Seed friendly initial welcome message
            Messages.Add(new ChatMessage
            {
                Sender = "Ramaverse Studio",
                Message = "Unified live chat connected. Stream messages will appear here in real-time.",
                Platform = ChatPlatform.System
            });
        }

        public void AddMessage(string sender, string message, ChatPlatform platform, bool isSub = false, bool isMod = false)
        {
            _dispatcher.InvokeAsync(() =>
            {
                Messages.Add(new ChatMessage
                {
                    Sender = sender,
                    Message = message,
                    Platform = platform,
                    IsSubscriber = isSub,
                    IsModerator = isMod
                });

                while (Messages.Count > 100)
                {
                    Messages.RemoveAt(0);
                }
            });
        }

        public void TriggerAlert(string title, string details, string amount = "")
        {
            _dispatcher.InvokeAsync(() =>
            {
                Alerts.Insert(0, new StreamAlert
                {
                    Title = title,
                    Details = details,
                    Amount = amount
                });

                while (Alerts.Count > 20)
                {
                    Alerts.RemoveAt(Alerts.Count - 1);
                }
            });
        }
    }
}
