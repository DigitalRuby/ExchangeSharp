using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public class WebSocketWrapper : IDisposable
    {
        private const int ReceiveChunkSize = 8192;
        private const int SendChunkSize = 1024;

        private ClientWebSocket _ws;
        private readonly Uri _uri;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken _cancellationToken;

        private Action<string, WebSocketWrapper> _onMessage;
        private Action<WebSocketWrapper> _onConnected;
        private Action<WebSocketWrapper> _onDisconnected;
        private bool _autoReconnect;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="uri">Uri to connect to</param>
        /// <param name="onMessage">Message callback</param>
        /// <param name="keepAlive">Keep alive time, 30 secnods is a good default</param>
        /// <param name="autoReconnect">Whether to attempt to auto-reconnect if disconnected</param>
        /// <param name="onConnect">Connect callback</param>
        /// <param name="onDisconnect">Disconnect callback</param>
        public WebSocketWrapper(string uri, Action<string, WebSocketWrapper> onMessage, TimeSpan keepAlive, bool autoReconnect = true,
            Action<WebSocketWrapper> onConnect = null, Action<WebSocketWrapper> onDisconnect = null)
        {
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = keepAlive;
            _uri = new Uri(uri);
            _cancellationToken = _cancellationTokenSource.Token;
            _autoReconnect = autoReconnect;
            _onMessage = onMessage;
            _onConnected = onConnect;
            _onDisconnected = onDisconnect;
        }

        /// <summary>
        /// Close and dispose of all resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                _autoReconnect = false;
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", CancellationToken.None).Wait();
            }
            catch
            {
            }
        }

        /// <summary>
        /// Begins connecting to the web socket server and starts listening. Returns immediately and does not wait for the connection to finish connecting.
        /// </summary>
        /// <returns>Task</returns>
        public void Connect()
        {
            _ws.ConnectAsync(_uri, _cancellationToken).ContinueWith((task) =>
            {
                CallOnConnected();
                Task.Factory.StartNew(ListenWorkerThread);
            });
        }

        /// <summary>
        /// Send a message to the WebSocket server.
        /// </summary>
        /// <param name="message">The message to send</param>
        public void SendMessage(string message)
        {
            SendMessageAsync(message).Wait();
        }

        private async Task SendMessageAsync(string message)
        {
            if (_ws.State != WebSocketState.Open)
            {
                throw new APIException("Connection is not open.");
            }

            var messageBuffer = Encoding.UTF8.GetBytes(message);
            var messagesCount = (int)Math.Ceiling((double)messageBuffer.Length / SendChunkSize);

            for (var i = 0; i < messagesCount; i++)
            {
                var offset = (SendChunkSize * i);
                var count = SendChunkSize;
                var lastMessage = ((i + 1) == messagesCount);

                if ((count * (i + 1)) > messageBuffer.Length)
                {
                    count = messageBuffer.Length - offset;
                }

                await _ws.SendAsync(new ArraySegment<byte>(messageBuffer, offset, count), WebSocketMessageType.Text, lastMessage, _cancellationToken);
            }
        }

        private void ListenWorkerThread()
        {
            var buffer = new byte[ReceiveChunkSize];
            bool wasClosed = false;
            TimeSpan keepAlive = _ws.Options.KeepAliveInterval;
            while (_autoReconnect)
            {
                try
                {
                    if (wasClosed)
                    {
                        wasClosed = false;
                        _ws = new ClientWebSocket();
                        _ws.ConnectAsync(_uri, CancellationToken.None).Wait();
                    }

                    while (_ws.State == WebSocketState.Open)
                    {
                        Task<WebSocketReceiveResult> result;
                        StringBuilder stringResult = new StringBuilder();

                        do
                        {
                            result = _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationToken);
                            result.Wait();
                            if (result.Result.MessageType == WebSocketMessageType.Close)
                            {
                                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();
                                CallOnDisconnected();
                            }
                            else
                            {
                                var str = Encoding.UTF8.GetString(buffer, 0, result.Result.Count);
                                stringResult.Append(str);
                            }

                        }
                        while (!result.Result.EndOfMessage);
                        if (stringResult.Length != 0)
                        {
                            CallOnMessage(stringResult);
                        }
                    }
                }
                catch
                {
                    CallOnDisconnected();
                    if (_autoReconnect)
                    {
                        // wait one second before attempting reconnect
                        Task.Delay(1000).Wait();
                    }
                }
                finally
                {
                    wasClosed = true;
                    _ws.Dispose();
                }
            }
        }

        private void CallOnMessage(StringBuilder stringResult)
        {
            if (_onMessage != null)
            {
                RunInTask(() => _onMessage(stringResult.ToString(), this));
            }
        }

        private void CallOnDisconnected()
        {
            if (_onDisconnected != null)
            {
                RunInTask(() => _onDisconnected(this));
            }
        }

        private void CallOnConnected()
        {
            if (_onConnected != null)
            {
                RunInTask(() => _onConnected(this));
            }
        }

        private static Task RunInTask(Action action)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    action();
                }
                catch
                {
                }
            });
        }
    }
}