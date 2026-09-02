using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace RamaverseStudio.Services
{
    /// <summary>
    /// High-performance built-in LAN Touch Deck server for Ramaverse Studio.
    /// Hosts the responsive Mobile Touch Deck SPA on port 4455, handles sub-5ms WebSocket IPC,
    /// and processes drag-and-drop soundboard uploads directly from mobile devices.
    /// </summary>
    public class RemoteControlServer : IDisposable
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly Dispatcher _uiDispatcher;
        private readonly List<WebSocket> _activeSockets = new();
        private readonly object _socketsLock = new();

        public int Port { get; private set; } = 4455;
        public bool IsRunning => _listener?.IsListening == true;
        public string LocalIpAddress { get; private set; } = "127.0.0.1";
        public string ServerUrl => $"http://{LocalIpAddress}:{Port}/";

        public Func<object>? GetStatusCallback { get; set; }
        public Action<string, string?>? ExecuteActionCallback { get; set; }
        public Func<string, byte[], string, bool>? UploadSoundCallback { get; set; }

        public RemoteControlServer(Dispatcher uiDispatcher, int port = 4455)
        {
            _uiDispatcher = uiDispatcher;
            Port = port;
            LocalIpAddress = ResolveLocalIpAddress();
        }

        public void Start()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _listener = new HttpListener();

            try
            {
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Start();
            }
            catch
            {
                _listener.Close();
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
                if (LocalIpAddress != "127.0.0.1")
                {
                    try { _listener.Prefixes.Add($"http://{LocalIpAddress}:{Port}/"); } catch { }
                }
                try
                {
                    _listener.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RemoteControlServer] Start failed: {ex.Message}");
                    return;
                }
            }

            _ = Task.Run(() => ListenLoopAsync(_cts.Token));
        }

        private async Task ListenLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context, ct), ct);
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception) { }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken ct)
        {
            var req = context.Request;
            var res = context.Response;

            if (req.IsWebSocketRequest)
            {
                try
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    _ = Task.Run(() => HandleWebSocketAsync(wsContext.WebSocket, ct), ct);
                }
                catch
                {
                    res.StatusCode = 500;
                    res.Close();
                }
                return;
            }

            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Content-Disposition, X-Sound-Name, X-Sound-Icon");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            string path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "/";

            try
            {
                if (path == "/" || path == "/index.html")
                {
                    byte[] htmlBytes = Encoding.UTF8.GetBytes(TouchDeckSpaSource);
                    res.ContentType = "text/html; charset=utf-8";
                    res.ContentLength64 = htmlBytes.Length;
                    await res.OutputStream.WriteAsync(htmlBytes, 0, htmlBytes.Length, ct);
                }
                else if (path == "/api/status")
                {
                    object statusObj = GetStatusCallback != null ? GetStatusCallback() : new { status = "online" };
                    string json = JsonSerializer.Serialize(statusObj);
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    res.ContentType = "application/json";
                    res.ContentLength64 = bytes.Length;
                    await res.OutputStream.WriteAsync(bytes, 0, bytes.Length, ct);
                }
                else if (path == "/api/action" && req.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
                    string body = await reader.ReadToEndAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    string action = root.TryGetProperty("action", out var actProp) ? actProp.GetString() ?? "" : "";
                    string? param = root.TryGetProperty("param", out var paramProp) ? paramProp.GetString() : null;

                    if (!string.IsNullOrEmpty(action))
                    {
                        _uiDispatcher.Invoke(() =>
                        {
                            ExecuteActionCallback?.Invoke(action, param);
                        });
                    }

                    byte[] okBytes = Encoding.UTF8.GetBytes("{\"success\":true}");
                    res.ContentType = "application/json";
                    res.ContentLength64 = okBytes.Length;
                    await res.OutputStream.WriteAsync(okBytes, 0, okBytes.Length, ct);
                }
                else if (path == "/api/upload_sound" && req.HttpMethod == "POST")
                {
                    string soundName = req.Headers["X-Sound-Name"] ?? "Custom SFX";
                    using var ms = new MemoryStream();
                    await req.InputStream.CopyToAsync(ms, ct);
                    byte[] fileBytes = ms.ToArray();

                    if (fileBytes.Length > 0)
                    {
                        string storageDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RamaverseStudio", "Soundboard");
                        Directory.CreateDirectory(storageDir);
                        string fileName = $"{Guid.NewGuid():N}.wav";
                        string savedFilePath = Path.Combine(storageDir, fileName);

                        await File.WriteAllBytesAsync(savedFilePath, fileBytes, ct);

                        _uiDispatcher.Invoke(() =>
                        {
                            UploadSoundCallback?.Invoke(soundName, fileBytes, savedFilePath);
                        });

                        byte[] respBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { success = true, filePath = savedFilePath, name = soundName }));
                        res.ContentType = "application/json";
                        res.ContentLength64 = respBytes.Length;
                        await res.OutputStream.WriteAsync(respBytes, 0, respBytes.Length, ct);
                    }
                    else
                    {
                        res.StatusCode = 400;
                    }
                }
                else
                {
                    res.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                res.StatusCode = 500;
                byte[] errBytes = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}");
                try { await res.OutputStream.WriteAsync(errBytes, 0, errBytes.Length, ct); } catch { }
            }
            finally
            {
                try { res.Close(); } catch { }
            }
        }

        private async Task HandleWebSocketAsync(WebSocket ws, CancellationToken ct)
        {
            lock (_socketsLock) _activeSockets.Add(ws);
            var buffer = new byte[8192];

            if (GetStatusCallback != null)
            {
                var initialStatus = GetStatusCallback();
                string initialJson = JsonSerializer.Serialize(initialStatus);
                byte[] initBytes = Encoding.UTF8.GetBytes(initialJson);
                await ws.SendAsync(new ArraySegment<byte>(initBytes), WebSocketMessageType.Text, true, ct);
            }

            try
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", ct);
                        break;
                    }

                    string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        using var doc = JsonDocument.Parse(msg);
                        var root = doc.RootElement;
                        string action = root.TryGetProperty("action", out var actProp) ? actProp.GetString() ?? "" : "";
                        string? param = root.TryGetProperty("param", out var paramProp) ? paramProp.GetString() : null;

                        if (!string.IsNullOrEmpty(action))
                        {
                            _uiDispatcher.Invoke(() =>
                            {
                                ExecuteActionCallback?.Invoke(action, param);
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                lock (_socketsLock) _activeSockets.Remove(ws);
                try { ws.Dispose(); } catch { }
            }
        }

        public async Task BroadcastStateAsync(object state)
        {
            string json = JsonSerializer.Serialize(state);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var segment = new ArraySegment<byte>(bytes);

            List<WebSocket> socketsCopy;
            lock (_socketsLock) socketsCopy = new List<WebSocket>(_activeSockets);

            foreach (var ws in socketsCopy)
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch { }
                }
            }
        }

        private static string ResolveLocalIpAddress()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                {
                    return endPoint.Address.ToString();
                }
            }
            catch { }
            return "127.0.0.1";
        }

        public void Dispose()
        {
            _cts?.Cancel();
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;

            lock (_socketsLock)
            {
                foreach (var ws in _activeSockets)
                {
                    try { ws.Dispose(); } catch { }
                }
                _activeSockets.Clear();
            }
        }

        private const string TouchDeckSpaSource = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>RAMAVERSE TOUCH DECK</title>
    <style>
        :root {
            --bg: #040307;
            --surface: #0F0D1A;
            --card: #140E26;
            --border: #2D224D;
            --accent: #7C3AED;
            --accent-glow: #C084FC;
            --text: #F3F4F6;
            --text-dim: #9CA3AF;
            --danger: #EF4444;
            --success: #10B981;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; user-select: none; -webkit-user-select: none; font-family: -apple-system, system-ui, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        body { background: var(--bg); color: var(--text); min-height: 100vh; display: flex; flex-direction: column; padding: 12px; }
        
        header { display: flex; justify-content: space-between; align-items: center; padding: 8px 12px; background: var(--surface); border: 1px solid var(--border); border-radius: 12px; margin-bottom: 12px; }
        .logo-title { font-weight: 900; font-size: 14px; letter-spacing: 1px; color: var(--accent-glow); }
        .status-badge { font-size: 11px; font-weight: 700; padding: 3px 8px; border-radius: 6px; background: rgba(16, 185, 129, 0.2); color: var(--success); }
        
        .deck-grid { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; flex-grow: 1; }
        @media (min-width: 480px) { .deck-grid { grid-template-columns: repeat(5, 1fr); } }

        .pad {
            background: var(--card);
            border: 1px solid var(--border);
            border-radius: 14px;
            aspect-ratio: 1;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            gap: 6px;
            cursor: pointer;
            transition: transform 0.08s ease, background 0.15s ease, border-color 0.15s ease;
            box-shadow: 0 4px 12px rgba(0,0,0,0.5);
        }
        .pad:active { transform: scale(0.92); background: #1F153B; border-color: var(--accent); }
        .pad-icon { font-size: 26px; }
        .pad-label { font-size: 11px; font-weight: 700; text-align: center; }

        .pad.rec-active { background: rgba(239, 68, 68, 0.2); border-color: var(--danger); box-shadow: 0 0 16px rgba(239, 68, 68, 0.4); }
        .pad.live-active { background: rgba(124, 58, 237, 0.25); border-color: var(--accent-glow); box-shadow: 0 0 16px rgba(192, 132, 252, 0.4); }
    </style>
</head>
<body>
    <header>
        <span class=""logo-title"">RAMAVERSE TOUCH DECK</span>
        <span id=""statusBadge"" class=""status-badge"">ONLINE</span>
    </header>

    <div class=""deck-grid"">
        <div id=""padRec"" class=""pad"" onclick=""triggerAction('toggle_record')"">
            <span class=""pad-icon"">⏺</span>
            <span class=""pad-label"">RECORD</span>
        </div>
        <div id=""padStream"" class=""pad"" onclick=""triggerAction('toggle_stream')"">
            <span class=""pad-icon"">📡</span>
            <span class=""pad-label"">GO LIVE</span>
        </div>
        <div class=""pad"" onclick=""triggerAction('trigger_replay')"">
            <span class=""pad-icon"">⚡</span>
            <span class=""pad-label"">SAVE CLIP</span>
        </div>
        <div class=""pad"" onclick=""triggerAction('take_snapshot')"">
            <span class=""pad-icon"">📸</span>
            <span class=""pad-label"">SNAPSHOT</span>
        </div>
        <div class=""pad"" onclick=""triggerAction('toggle_mute_mic')"">
            <span class=""pad-icon"">🎤</span>
            <span class=""pad-label"">MUTE MIC</span>
        </div>

        <div class=""pad"" onclick=""triggerAction('play_sfx', 'AirHorn')"">
            <span class=""pad-icon"">📢</span>
            <span class=""pad-label"">AIR HORN</span>
        </div>
        <div class=""pad"" onclick=""triggerAction('play_sfx', 'Applause')"">
            <span class=""pad-icon"">👏</span>
            <span class=""pad-label"">APPLAUSE</span>
        </div>
        <div class=""pad"" onclick=""triggerAction('play_sfx', 'VictoryChime')"">
            <span class=""pad-icon"">🏆</span>
            <span class=""pad-label"">VICTORY</span>
        </div>
        <div class=""pad"" onclick=""triggerAction('play_sfx', 'Laser')"">
            <span class=""pad-icon"">🔫</span>
            <span class=""pad-label"">LASER</span>
        </div>
        <div class=""pad"" onclick=""triggerAction('stop_all_sfx')"">
            <span class=""pad-icon"">⏹</span>
            <span class=""pad-label"">STOP SFX</span>
        </div>
    </div>

    <script>
        let ws;
        function connect() {
            const loc = window.location;
            const wsUrl = (loc.protocol === 'https:' ? 'wss://' : 'ws://') + loc.host + '/ws';
            ws = new WebSocket(wsUrl);

            ws.onopen = () => {
                document.getElementById('statusBadge').innerText = 'CONNECTED';
                document.getElementById('statusBadge').style.background = 'rgba(16, 185, 129, 0.2)';
            };
            ws.onclose = () => {
                document.getElementById('statusBadge').innerText = 'OFFLINE';
                document.getElementById('statusBadge').style.background = 'rgba(239, 68, 68, 0.2)';
                setTimeout(connect, 2000);
            };
            ws.onmessage = (e) => {
                try {
                    const data = JSON.parse(e.data);
                    const padRec = document.getElementById('padRec');
                    if (data.isRecording) padRec.classList.add('rec-active');
                    else padRec.classList.remove('rec-active');

                    const padStream = document.getElementById('padStream');
                    if (data.isStreaming) padStream.classList.add('live-active');
                    else padStream.classList.remove('live-active');
                } catch(err){}
            };
        }

        function triggerAction(action, param = null) {
            try { if (navigator.vibrate) navigator.vibrate(12); } catch(e){}
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ action: action, param: String(param) }));
            }
        }
        connect();
    </script>
</body>
</html>";
    }
}
