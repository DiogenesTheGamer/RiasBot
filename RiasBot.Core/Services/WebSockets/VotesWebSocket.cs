using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RiasBot.Commons.Configs;

namespace RiasBot.Services.Websockets
{
    public class VotesWebsocket
    {
        private bool _connected;
        private ClientWebSocket _webSocket;
        private readonly Uri _hostUri;
        private readonly VotesManagerConfig _config;
        private readonly RLog _log;

        public event Func<JObject, Task> OnReceive;

        public event Func<WebSocketCloseStatus?, string, Task> OnClosed;

        public event Func<Task> OnConnected;

        public VotesWebsocket(VotesManagerConfig config, RLog log)
        {
            _config = config;
            _log = log;
            var connectionType = config.IsSecureConnection ? "wss" : "ws";
            _hostUri = new Uri($"{connectionType}://{config.WebSocketHost}:{config.WebSocketPort}/{config.UrlParameters}");
        }

        private async Task TryConnectWebSocketAsync()
        {
            while (!IsConnected())
            {
                try
                {
                    await ConnectWebSocketAsync();
                }
                catch
                {
                    await _log.Error("The VotesWebSocket connection was closed or aborted! Attempting reconnect in 30 seconds");
                    _connected = false;
                    await Task.Delay(30 * 1000);
                    await Connect();
                    break;
                }
            }
        }

        private async Task ConnectWebSocketAsync()
        {
            _webSocket = new ClientWebSocket();
            _webSocket.Options.SetRequestHeader("Authorization", _config.Authorization);

            await _webSocket.ConnectAsync(_hostUri, CancellationToken.None);
            await _log.Info("VotesWebSocket connected");
            if (OnConnected != null) await OnConnected.Invoke();
            _connected = true;
            while (_webSocket.State == WebSocketState.Open)
            {
                var jsonString = await ReceiveAsync(_webSocket);
                var json = JObject.Parse(jsonString);
                if (OnReceive != null) await OnReceive.Invoke(json);
            }

            var unused = new Timer(async _ => await Connect(), null, new TimeSpan(0, 0, 30), TimeSpan.Zero);
        }

        private async Task DisconnectWebSocketAsync()
        {
            if (_webSocket == null)
                return;
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }

        private async Task<string> ReceiveAsync(ClientWebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var end = false;
            while (!end)
            {
                var socketReceiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                var result = socketReceiveResult;
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    if (OnClosed != null)
                        await OnClosed.Invoke(result.CloseStatus, result.CloseStatusDescription);
                    _connected = false;
                    await _log.Warning("VotesWebSocket disconnected");
                }
                else
                {
                    if (result.EndOfMessage)
                        end = true;
                }
            }
            return Encoding.UTF8.GetString(buffer);
        }

        public bool IsConnected()
        {
            return _webSocket != null && _connected;
        }

        public async Task SendAsync(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            await _webSocket.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public Task Connect()
        {
            _ = Task.Run(async () => await TryConnectWebSocketAsync());
            return Task.CompletedTask;
        }

        public Task Disconnect()
        {
            _ = Task.Run(async () => await DisconnectWebSocketAsync());
            return Task.CompletedTask;
        }

        public string GetHostUri()
        {
            return _hostUri.AbsoluteUri;
        }
    }
}