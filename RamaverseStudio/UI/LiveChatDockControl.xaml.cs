using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RamaverseStudio.Services;

namespace RamaverseStudio.UI
{
    public partial class LiveChatDockControl : UserControl
    {
        private ChatAggregatorService? _chatService;
        private readonly Random _random = new Random();

        public LiveChatDockControl()
        {
            InitializeComponent();
        }

        public void BindService(ChatAggregatorService service)
        {
            _chatService = service;
            ChatItemsControl.ItemsSource = _chatService.Messages;
            _chatService.Messages.CollectionChanged += (s, e) =>
            {
                ChatScrollViewer.ScrollToEnd();
            };
        }

        private void OnSendMessageClicked(object sender, RoutedEventArgs e)
        {
            SendBroadcasterMessage();
        }

        private void OnChatInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SendBroadcasterMessage();
                e.Handled = true;
            }
        }

        private void SendBroadcasterMessage()
        {
            if (_chatService == null || string.IsNullOrWhiteSpace(TxtChatMessage.Text)) return;
            string text = TxtChatMessage.Text.Trim();
            _chatService.AddMessage("Broadcaster (You)", text, ChatPlatform.System, isSub: true, isMod: true);
            TxtChatMessage.Clear();
        }

        private void OnTestAlertClicked(object sender, RoutedEventArgs e)
        {
            if (_chatService == null) return;
            string[] viewers = { "ShadowGamer99", "PixelQueen", "NeonVortex", "CyberSamurai", "AlphaStrike" };
            string name = viewers[_random.Next(viewers.Length)];
            int alertType = _random.Next(3);

            if (alertType == 0)
            {
                _chatService.AddMessage(name, "Just subscribed to the channel! Let's GO! 🎉", ChatPlatform.YouTube, isSub: true);
                _chatService.TriggerAlert("⭐ NEW SUBSCRIBER!", $"{name} joined the squad!");
            }
            else if (alertType == 1)
            {
                _chatService.AddMessage(name, "Sent a $10.00 SuperChat: GG WP bro love the stream!! 💸", ChatPlatform.YouTube, isSub: true);
                _chatService.TriggerAlert("💰 SUPERCHAT!", $"{name} sent $10.00!", "$10.00");
            }
            else
            {
                _chatService.AddMessage(name, "Raiding with 45 viewers! 🚀", ChatPlatform.Twitch);
                _chatService.TriggerAlert("🚀 INCOMING RAID!", $"{name} raiding with 45 viewers!");
            }
        }

        private void OnClearChatClicked(object sender, RoutedEventArgs e)
        {
            _chatService?.Messages.Clear();
        }
    }
}
