using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RamaverseStudio.Services
{
    public enum ChatPlatform
    {
        YouTube,
        Twitch,
        Kick,
        Facebook,
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
        public bool IsBroadcaster { get; set; } = false;
        public string PlatformLetter { get; set; } = "S";
    }

    /// <summary>
    /// Rich alert events parsed from Twitch IRC tags (USERNOTICE / PRIVMSG bits).
    /// The UI can render these as native alert-box overlays.
    /// </summary>
    public enum AlertKind
    {
        Subscription,
        Raid,
        BitsCheer,
        Follow  // reserved for EventSub/API
    }

    public class AlertEvent
    {
        public AlertKind Kind { get; init; }
        public string Username { get; init; } = "";
        public string Details { get; init; } = "";
        public string Amount { get; init; } = "";
    }

    public class StreamAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "New Subscriber!";
        public string Details { get; set; } = "Viewer subscribed to channel";
        public string Amount { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public AlertKind Kind { get; set; } = AlertKind.Subscription;
    }

    /// <summary>
    /// Unified chat hub. Aggregates messages from connected platforms (currently
    /// Twitch IRC) with system and broadcaster messages, capped at 100 entries.
    /// </summary>
    public class ChatAggregatorService : IDisposable
    {
        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();
        public ObservableCollection<StreamAlert> Alerts { get; } = new ObservableCollection<StreamAlert>();

        private readonly Dispatcher? _dispatcher;

        public event Action<ChatMessage>? MessageReceived;

        public ChatAggregatorService(Dispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher;

            // Seed friendly initial welcome message
            Messages.Add(new ChatMessage
            {
                Sender = "Ramaverse Studio",
                Message = "Live chat ready. Connect your Twitch channel in the Chat dock to receive real-time viewer messages.",
                Platform = ChatPlatform.System
            });
        }

        public void AddMessage(string sender, string message, ChatPlatform platform, bool isSub = false, bool isMod = false, bool isBroadcaster = false)
        {
            void Append()
            {
                var msg = new ChatMessage
                {
                    Sender = sender,
                    Message = message,
                    Platform = platform,
                    IsSubscriber = isSub,
                    IsModerator = isMod,
                    IsBroadcaster = isBroadcaster,
                    PlatformLetter = platform switch
                    {
                        ChatPlatform.Twitch => "T",
                        ChatPlatform.YouTube => "Y",
                        ChatPlatform.Kick => "K",
                        ChatPlatform.Facebook => "F",
                        _ => "S"
                    }
                };

                Messages.Add(msg);

                while (Messages.Count > 100)
                {
                    Messages.RemoveAt(0);
                }

                MessageReceived?.Invoke(msg);
            }

            if (_dispatcher != null)
                _dispatcher.InvokeAsync(Append);
            else
                Append();
        }

        public void TriggerAlert(string title, string details, string amount = "")
        {
            void AppendAlert()
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
            }

            if (_dispatcher != null)
                _dispatcher.InvokeAsync(AppendAlert);
            else
                AppendAlert();

            // Also surface as a rich event for the canvas alert overlay.
            AlertRaised?.Invoke(new StreamAlert
            {
                Title = title,
                Details = details,
                Amount = amount
            });
        }

        /// <summary>
        /// Fired for every alert (test or live platform event) so the canvas
        /// overlay source can animate it.
        /// </summary>
        public event Action<StreamAlert>? AlertRaised;

        public void Dispose()
        {
            _twitch?.Dispose();
            _twitch = null;
        }

        private TwitchIrcClient? _twitch;

        /// <summary>
        /// Connects to Twitch IRC anonymously (read-only chat) using just the
        /// channel name — no OAuth token needed to read chat.
        /// </summary>
        public async Task<bool> ConnectTwitchAsync(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return false;
            }

            _twitch?.Dispose();
            _twitch = new TwitchIrcClient(OnTwitchMessage, OnTwitchSystem, OnTwitchAlert);
            bool ok = await _twitch.ConnectAsync(channelName.Trim().ToLowerInvariant());
            if (ok)
            {
                AddMessage("System", $"Connected to #{channelName.Trim().ToLowerInvariant()} chat. Messages will appear live.", ChatPlatform.System);
            }
            else
            {
                AddMessage("System", $"Could not connect to Twitch chat for #{channelName.Trim().ToLowerInvariant()}. Check the channel name and your internet connection.", ChatPlatform.System);
            }
            return ok;
        }

        private void OnTwitchAlert(AlertEvent alert)
        {
            string title = alert.Kind switch
            {
                AlertKind.Subscription => "NEW SUBSCRIBER!",
                AlertKind.Raid => "INCOMING RAID!",
                AlertKind.BitsCheer => "BITS CHEER!",
                _ => "NEW FOLLOWER!"
            };

            string details = alert.Kind switch
            {
                AlertKind.BitsCheer => $"{alert.Username} cheered {alert.Amount} bits!",
                _ => alert.Details
            };

            _dispatcher?.InvokeAsync(() =>
            {
                Alerts.Insert(0, new StreamAlert
                {
                    Title = title,
                    Details = details,
                    Amount = alert.Amount,
                    Kind = alert.Kind
                });
                while (Alerts.Count > 20)
                {
                    Alerts.RemoveAt(Alerts.Count - 1);
                }
            });

            AlertRaised?.Invoke(new StreamAlert
            {
                Title = title,
                Details = details,
                Amount = alert.Amount,
                Kind = alert.Kind
            });
        }

        public void DisconnectTwitch()
        {
            _twitch?.Dispose();
            _twitch = null;
            AddMessage("System", "Twitch chat disconnected.", ChatPlatform.System);
        }

        public bool IsTwitchConnected => _twitch?.IsConnected ?? false;
        public string? ConnectedTwitchChannel => _twitch?.Channel;

        private YouTubeLiveChatClient? _youtube;

        public async Task<bool> ConnectYouTubeAsync(string videoIdOrUrl)
        {
            if (string.IsNullOrWhiteSpace(videoIdOrUrl)) return false;

            _youtube?.Dispose();
            _youtube = new YouTubeLiveChatClient(OnYouTubeMessage, OnYouTubeSystem, OnYouTubeAlert);
            bool ok = await _youtube.ConnectAsync(videoIdOrUrl.Trim());
            if (ok)
            {
                AddMessage("System", $"Connected to YouTube live chat ({_youtube.VideoId}).", ChatPlatform.System);
            }
            else
            {
                AddMessage("System", $"Could not connect to YouTube live chat for '{videoIdOrUrl}'. Make sure the stream is public and live.", ChatPlatform.System);
            }
            return ok;
        }

        public void DisconnectYouTube()
        {
            _youtube?.Dispose();
            _youtube = null;
            AddMessage("System", "YouTube chat disconnected.", ChatPlatform.System);
        }

        public bool IsYouTubeConnected => _youtube?.IsConnected ?? false;
        public string? ConnectedYouTubeChannel => _youtube?.VideoId;

        private void OnYouTubeMessage(string sender, string text, bool isMod, bool isSub, bool isBroadcaster)
        {
            AddMessage(sender, text, ChatPlatform.YouTube, isSub, isMod, isBroadcaster);
        }

        private void OnYouTubeSystem(string text)
        {
            AddMessage("YouTube", text, ChatPlatform.System);
        }

        private void OnYouTubeAlert(AlertEvent alert)
        {
            OnTwitchAlert(alert);
        }

        private KickChatClient? _kick;

        public async Task<bool> ConnectKickAsync(string channelSlug)
        {
            if (string.IsNullOrWhiteSpace(channelSlug)) return false;

            _kick?.Dispose();
            _kick = new KickChatClient(OnKickMessage, OnKickSystem, OnKickAlert);
            bool ok = await _kick.ConnectAsync(channelSlug.Trim().ToLowerInvariant());
            if (ok)
            {
                AddMessage("System", $"Connected to Kick chat ({_kick.Channel}).", ChatPlatform.System);
            }
            else
            {
                AddMessage("System", $"Could not connect to Kick chat for '{channelSlug}'. Check the channel name.", ChatPlatform.System);
            }
            return ok;
        }

        public void DisconnectKick()
        {
            _kick?.Dispose();
            _kick = null;
            AddMessage("System", "Kick chat disconnected.", ChatPlatform.System);
        }

        public bool IsKickConnected => _kick?.IsConnected ?? false;
        public string? ConnectedKickChannel => _kick?.Channel;

        private void OnKickMessage(string sender, string text, bool isMod, bool isSub, bool isBroadcaster)
        {
            AddMessage(sender, text, ChatPlatform.Kick, isSub, isMod, isBroadcaster);
        }

        private void OnKickSystem(string text)
        {
            AddMessage("Kick", text, ChatPlatform.System);
        }

        private void OnKickAlert(AlertEvent alert)
        {
            OnTwitchAlert(alert);
        }

        private void OnTwitchMessage(string sender, string text, bool isMod, bool isSub, bool isBroadcaster)
        {
            AddMessage(sender, text, ChatPlatform.Twitch, isSub, isMod, isBroadcaster);
        }

        private void OnTwitchSystem(string text)
        {
            AddMessage("Twitch", text, ChatPlatform.System);
        }
    }

    /// <summary>
    /// Minimal Twitch IRC (IRCv3 over TLS) client for reading channel chat.
    /// Uses an anonymous login, which Twitch permits for read-only chat.
    /// Parses PRIVMSG (chat) and USERNOTICE (subs/raids/bits) events.
    /// </summary>
    internal sealed class TwitchIrcClient : IDisposable
    {
        private const string Host = "irc.chat.twitch.tv";
        private const int Port = 6697;
        private const string AnonymousNickPrefix = "justinfan";

        private readonly Action<string, string, bool, bool, bool> _onMessage;
        private readonly Action<string> _onSystem;
        private readonly Action<AlertEvent> _onAlert;
        private TcpClient? _tcp;
        private SslStream? _stream;
        private CancellationTokenSource? _cts;
        private Task? _readTask;

        public bool IsConnected => _tcp?.Connected ?? false;
        public string? Channel { get; private set; }

        public TwitchIrcClient(Action<string, string, bool, bool, bool> onMessage,
                               Action<string> onSystem,
                               Action<AlertEvent> onAlert)
        {
            _onMessage = onMessage;
            _onSystem = onSystem;
            _onAlert = onAlert;
        }

        public async Task<bool> ConnectAsync(string channel)
        {
            try
            {
                _cts = new CancellationTokenSource();
                _tcp = new TcpClient();
                await _tcp.ConnectAsync(Host, Port);
                _tcp.ReceiveTimeout = 300000; // PING keepalive covers gaps

                _stream = new SslStream(_tcp.GetStream(), false, (_, _, _, _) => true);
                await _stream.AuthenticateAsClientAsync(Host);
                if (!_stream.IsEncrypted) return false;

                string nick = AnonymousNickPrefix + RandomDigits();
                var hello = new StringBuilder();
                hello.Append("PASS SCHMOOPIIE\r\n");
                hello.Append($"NICK {nick}\r\n");
                hello.Append("CAP REQ :twitch.tv/tags twitch.tv/membership\r\n");
                Channel = channel;
                await SendAsync(hello.ToString());
                await SendAsync($"JOIN #{channel}\r\n");

                _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Twitch connect failed: {ex.Message}");
                return false;
            }
        }

        private static string RandomDigits()
        {
            var rng = Random.Shared;
            int n = rng.Next(10000, 99999);
            return n.ToString();
        }

        private async Task SendAsync(string text)
        {
            if (_stream != null && _stream.CanWrite)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                await _stream.WriteAsync(bytes);
                await _stream.FlushAsync();
            }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[8192];
            var lineBuf = new StringBuilder(1024);

            try
            {
                while (!ct.IsCancellationRequested && _stream != null)
                {
                    int read = await _stream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (read == 0) break; // server closed

                    for (int i = 0; i < read; i++)
                    {
                        char c = (char)buffer[i];
                        if (c == '\n')
                        {
                            string line = lineBuf.ToString().TrimEnd('\r');
                            lineBuf.Clear();
                            HandleLine(line);
                        }
                        else
                        {
                            if (lineBuf.Length < 4096)
                            {
                                lineBuf.Append(c);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"Twitch read loop ended: {ex.Message}");
                _onSystem("Chat connection lost. Use Reconnect to try again.");
            }
        }

        private void HandleLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            // Keepalive
            if (line.StartsWith("PING", StringComparison.Ordinal))
            {
                _ = SendAsync("PONG :tmi.twitch.tv\r\n");
                return;
            }

            // USERNOTICE: subscriptions, raids, bits, ritual events
            if (line.Contains("USERNOTICE #", StringComparison.Ordinal))
            {
                HandleUserNotice(line);
                return;
            }

            // Tagged PRIVMSG: @badge-info=...;badges=... :user!user@user.tmi.twitch.tv PRIVMSG #chan :text
            string message = "";
            string sender = "";
            bool isMod = false, isSub = false, isBroadcaster = false;

            int privIdx = line.IndexOf("PRIVMSG #", StringComparison.Ordinal);
            if (privIdx < 0) return;

            int textStart = line.IndexOf(':', privIdx);
            if (textStart >= 0 && textStart + 1 < line.Length)
            {
                message = line[(textStart + 1)..];
            }

            // sender: between " :user!" and "!user@"
            int userStart = line.IndexOf(':', 0);
            if (userStart >= 0)
            {
                int bang = line.IndexOf('!', userStart);
                if (bang > userStart)
                {
                    sender = line[(userStart + 1)..bang];
                }
            }

            if (line[0] == '@')
            {
                int badgesIdx = line.IndexOf("badges=", StringComparison.Ordinal);
                if (badgesIdx >= 0)
                {
                    int end = line.IndexOf(';', badgesIdx);
                    string badges = end < 0 ? line[badgesIdx..textStart] : line[badgesIdx..end];
                    isBroadcaster = badges.Contains("broadcaster/1", StringComparison.Ordinal);
                    isMod = badges.Contains("moderator/1", StringComparison.Ordinal);
                    isSub = badges.Contains("subscriber/", StringComparison.Ordinal);
                }

                // Bits cheer: PRIVMSG with a bits=N tag
                int bitsIdx = line.IndexOf("bits=", StringComparison.Ordinal);
                if (bitsIdx >= 0 && !string.IsNullOrEmpty(sender))
                {
                    int end = line.IndexOf(';', bitsIdx);
                    string bitsVal = end < 0 ? line[bitsIdx..textStart] : line[bitsIdx..end];
                    bitsVal = bitsVal["bits=".Length..].Trim();
                    _onAlert(new AlertEvent
                    {
                        Kind = AlertKind.BitsCheer,
                        Username = sender,
                        Details = message,
                        Amount = bitsVal
                    });
                }
            }

            if (!string.IsNullOrEmpty(sender) && !string.IsNullOrEmpty(message))
            {
                _onMessage(sender, message, isMod, isSub, isBroadcaster);
            }
        }

        /// <summary>
        /// Parses USERNOTICE events: sub/resub, gift subs, raids, and
        /// announcement notices using the msg-id tag.
        /// </summary>
        private void HandleUserNotice(string line)
        {
            string msgId = GetTag(line, "msg-id") ?? "";
            string login = GetTag(line, "login") ?? "";
            string? display = GetTag(line, "display-name");
            if (string.IsNullOrWhiteSpace(display)) display = login;
            if (string.IsNullOrWhiteSpace(display)) display = "Viewer";

            // Optional human message after the final ':'
            int lastColon = line.LastIndexOf(':');
            string systemMsg = lastColon >= 0 && lastColon + 1 < line.Length
                ? line[(lastColon + 1)..]
                : GetTag(line, "system-msg") ?? "";

            switch (msgId)
            {
                case "sub":
                case "resub":
                    string months = GetTag(line, "msg-param-cumulative-months") ?? "1";
                    _onAlert(new AlertEvent
                    {
                        Kind = AlertKind.Subscription,
                        Username = display,
                        Details = $"Subscribed for {months} month{(months == "1" ? "" : "s")}",
                        Amount = months
                    });
                    break;

                case "subgift":
                case "anonsubgift":
                    string recipient = GetTag(line, "msg-param-recipient-display-name") ?? "someone";
                    _onAlert(new AlertEvent
                    {
                        Kind = AlertKind.Subscription,
                        Username = display,
                        Details = $"Gifted a sub to {recipient}",
                        Amount = "gift"
                    });
                    break;

                case "raid":
                    string raiders = GetTag(line, "msg-param-viewerCount") ?? "?";
                    _onAlert(new AlertEvent
                    {
                        Kind = AlertKind.Raid,
                        Username = display,
                        Details = $"Raiding with {raiders} viewers",
                        Amount = raiders
                    });
                    break;
            }
        }

        /// <summary>
        /// Extracts a tag value from an IRCv3 tag section: "@k=v;k2=v2 :src...".
        /// Returns null when the tag is absent.
        /// </summary>
        private static string? GetTag(string line, string key)
        {
            int idx = line.IndexOf(key + "=", StringComparison.Ordinal);
            if (idx < 0) return null;

            int start = idx + key.Length + 1;
            int end = line.IndexOf(';', start);
            if (end < 0)
            {
                // last tag before the space separating tags from the prefix
                int space = line.IndexOf(' ', start);
                end = space < 0 ? line.Length : space;
            }
            return line[start..end].Replace("\\s", " ");
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcp?.Dispose(); } catch { }
            _stream = null;
            _tcp = null;
        }
    }

    /// <summary>
    /// Lightweight YouTube Live Chat client that polls the public chat stream
    /// without requiring an OAuth token.
    /// </summary>
    internal sealed class YouTubeLiveChatClient : IDisposable
    {
        private readonly Action<string, string, bool, bool, bool> _onMessage;
        private readonly Action<string> _onSystem;
        private readonly Action<AlertEvent> _onAlert;
        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private readonly System.Net.Http.HttpClient _http = new();

        public bool IsConnected { get; private set; }
        public string VideoId { get; private set; } = "";

        public YouTubeLiveChatClient(Action<string, string, bool, bool, bool> onMessage,
                                     Action<string> onSystem,
                                     Action<AlertEvent> onAlert)
        {
            _onMessage = onMessage;
            _onSystem = onSystem;
            _onAlert = onAlert;
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<bool> ConnectAsync(string input)
        {
            try
            {
                VideoId = ExtractVideoId(input);
                if (string.IsNullOrWhiteSpace(VideoId)) return false;

                _cts = new CancellationTokenSource();
                IsConnected = true;
                _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"YouTube connect error: {ex.Message}");
                return false;
            }
        }

        private static string ExtractVideoId(string input)
        {
            if (input.Length == 11 && !input.Contains('/') && !input.Contains('.')) return input;
            var match = System.Text.RegularExpressions.Regex.Match(input, @"(?:v=|\/live\/|\.be\/)([a-zA-Z0-9_-]{11})");
            return match.Success ? match.Groups[1].Value : input;
        }

        private async Task PollLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    // Delay between poll intervals
                    await Task.Delay(4000, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"YouTube chat ended: {ex.Message}");
            }
        }

        public void Dispose()
        {
            IsConnected = false;
            try { _cts?.Cancel(); } catch { }
            try { _http.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// Kick streaming chat client for receiving viewer messages and alerts.
    /// </summary>
    internal sealed class KickChatClient : IDisposable
    {
        private readonly Action<string, string, bool, bool, bool> _onMessage;
        private readonly Action<string> _onSystem;
        private readonly Action<AlertEvent> _onAlert;
        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private readonly System.Net.Http.HttpClient _http = new();

        public bool IsConnected { get; private set; }
        public string Channel { get; private set; } = "";

        public KickChatClient(Action<string, string, bool, bool, bool> onMessage,
                              Action<string> onSystem,
                              Action<AlertEvent> onAlert)
        {
            _onMessage = onMessage;
            _onSystem = onSystem;
            _onAlert = onAlert;
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<bool> ConnectAsync(string channel)
        {
            try
            {
                Channel = channel.Trim().ToLowerInvariant();
                _cts = new CancellationTokenSource();
                IsConnected = true;
                _pollTask = Task.Run(() => ListenLoopAsync(_cts.Token));
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Kick connect error: {ex.Message}");
                return false;
            }
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && IsConnected)
                {
                    await Task.Delay(5000, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.WriteLine($"Kick chat ended: {ex.Message}");
            }
        }

        public void Dispose()
        {
            IsConnected = false;
            try { _cts?.Cancel(); } catch { }
            try { _http.Dispose(); } catch { }
        }
    }
}
