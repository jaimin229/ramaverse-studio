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
        private bool _autoScroll = true;

        public LiveChatDockControl()
        {
            InitializeComponent();
            Loaded += (s, e) => RestoreScrollBehavior();
        }

        private void RestoreScrollBehavior()
        {
            // Track whether the viewer is at the bottom; only auto-scroll then.
            if (ChatScrollViewer != null)
            {
                ChatScrollViewer.ScrollChanged += (s2, e2) =>
                {
                    if (e2.ExtentHeightChange > 0 || e2.ViewportHeightChange > 0) return;
                    _autoScroll = e2.VerticalOffset >= e2.ExtentHeight - e2.ViewportHeight - 8;
                };
            }
        }

        public void BindService(ChatAggregatorService service)
        {
            _chatService = service;
            ChatItemsControl.ItemsSource = _chatService.Messages;
            _chatService.MessageReceived += OnMessageReceived;
        }

        private void OnMessageReceived(ChatMessage msg)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_autoScroll)
                {
                    ChatScrollViewer.ScrollToEnd();
                }
            });
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
            _chatService.AddMessage("Broadcaster (You)", text, ChatPlatform.System, isSub: true, isMod: true, isBroadcaster: true);
            TxtChatMessage.Clear();
        }

        private void OnPlatformSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePlatformStatus();
        }

        private async void OnConnectPlatformClicked(object sender, RoutedEventArgs e)
        {
            if (_chatService == null) return;

            int sel = ComboPlatform.SelectedIndex;
            string input = TxtChannelInput.Text.Trim();

            if (sel == 0) // Twitch
            {
                if (_chatService.IsTwitchConnected)
                {
                    _chatService.DisconnectTwitch();
                    UpdatePlatformStatus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    TxtChatStatus.Text = "Enter your Twitch channel name.";
                    return;
                }

                BtnConnectPlatform.IsEnabled = false;
                TxtChatStatus.Text = $"Connecting to Twitch #{input.TrimStart('#')}...";
                try
                {
                    bool ok = await _chatService.ConnectTwitchAsync(input.TrimStart('#'));
                    UpdatePlatformStatus();
                }
                finally
                {
                    BtnConnectPlatform.IsEnabled = true;
                }
            }
            else if (sel == 1) // YouTube
            {
                if (_chatService.IsYouTubeConnected)
                {
                    _chatService.DisconnectYouTube();
                    UpdatePlatformStatus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    TxtChatStatus.Text = "Enter YouTube live stream URL or Video ID.";
                    return;
                }

                BtnConnectPlatform.IsEnabled = false;
                TxtChatStatus.Text = "Connecting to YouTube live chat...";
                try
                {
                    bool ok = await _chatService.ConnectYouTubeAsync(input);
                    UpdatePlatformStatus();
                }
                finally
                {
                    BtnConnectPlatform.IsEnabled = true;
                }
            }
            else if (sel == 2) // Kick
            {
                if (_chatService.IsKickConnected)
                {
                    _chatService.DisconnectKick();
                    UpdatePlatformStatus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(input))
                {
                    TxtChatStatus.Text = "Enter Kick channel name.";
                    return;
                }

                BtnConnectPlatform.IsEnabled = false;
                TxtChatStatus.Text = $"Connecting to Kick channel {input}...";
                try
                {
                    bool ok = await _chatService.ConnectKickAsync(input);
                    UpdatePlatformStatus();
                }
                finally
                {
                    BtnConnectPlatform.IsEnabled = true;
                }
            }
        }

        private void UpdatePlatformStatus()
        {
            if (_chatService == null) return;

            int sel = ComboPlatform.SelectedIndex;
            if (sel == 0) // Twitch
            {
                if (_chatService.IsTwitchConnected)
                {
                    TxtChatStatus.Text = $"Twitch: connected to #{_chatService.ConnectedTwitchChannel}";
                    BtnConnectPlatform.Content = "Disconnect";
                }
                else
                {
                    TxtChatStatus.Text = "Twitch: offline";
                    BtnConnectPlatform.Content = "Connect";
                }
            }
            else if (sel == 1) // YouTube
            {
                if (_chatService.IsYouTubeConnected)
                {
                    TxtChatStatus.Text = $"YouTube: connected ({_chatService.ConnectedYouTubeChannel})";
                    BtnConnectPlatform.Content = "Disconnect";
                }
                else
                {
                    TxtChatStatus.Text = "YouTube: offline";
                    BtnConnectPlatform.Content = "Connect";
                }
            }
            else if (sel == 2) // Kick
            {
                if (_chatService.IsKickConnected)
                {
                    TxtChatStatus.Text = $"Kick: connected ({_chatService.ConnectedKickChannel})";
                    BtnConnectPlatform.Content = "Disconnect";
                }
                else
                {
                    TxtChatStatus.Text = "Kick: offline";
                    BtnConnectPlatform.Content = "Connect";
                }
            }
        }

        private void OnTestAlertClicked(object sender, RoutedEventArgs e)
        {
            if (_chatService == null) return;
            string[] viewers = { "ShadowGamer99", "PixelQueen", "NeonVortex", "CyberSamurai", "AlphaStrike" };
            string name = viewers[_random.Next(viewers.Length)];
            int alertType = _random.Next(3);

            if (alertType == 0)
            {
                _chatService.AddMessage(name, "Just subscribed to the channel! Let's GO!", ChatPlatform.Twitch, isSub: true);
                _chatService.TriggerAlert("NEW SUBSCRIBER!", $"{name} joined the squad!");
            }
            else if (alertType == 1)
            {
                _chatService.AddMessage(name, "Sent a $10.00 donation: GG WP bro love the stream!!", ChatPlatform.YouTube, isSub: true);
                _chatService.TriggerAlert("SUPERCHAT!", $"{name} sent $10.00!", "$10.00");
            }
            else
            {
                _chatService.AddMessage(name, "Raiding with 45 viewers!", ChatPlatform.Twitch);
                _chatService.TriggerAlert("INCOMING RAID!", $"{name} raiding with 45 viewers!");
            }
        }

        private void OnClearChatClicked(object sender, RoutedEventArgs e)
        {
            _chatService?.Messages.Clear();
        }
    }
}
