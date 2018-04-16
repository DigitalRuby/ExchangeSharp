﻿/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    public class WebSocketWrapper : IDisposable
    {
        private const int receiveChunkSize = 8192;

        private ClientWebSocket _ws;
        private readonly Uri _uri;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken _cancellationToken;
        private readonly BlockingCollection<object> _messageQueue = new BlockingCollection<object>(new ConcurrentQueue<object>());

        private System.Action<string, WebSocketWrapper> _onMessage;
        private System.Action<WebSocketWrapper> _onConnected;
        private System.Action<WebSocketWrapper> _onDisconnected;
        private TimeSpan _connectInterval;
        private bool _disposed;

        /// <summary>
        /// Constructor, also begins listening and processing messages immediately
        /// </summary>
        /// <param name="uri">Uri to connect to</param>
        /// <param name="onMessage">Message callback</param>
        /// <param name="keepAlive">Keep alive time, default is 30 seconds</param>
        /// <param name="onConnect">Connect callback, will get called on connection and every connectInterval (default 1 hour). This is a great place
        /// to do setup, such as creating lookup dictionaries, etc. This method will re-execute until it executes without exceptions thrown.</param>
        /// <param name="onDisconnect">Disconnect callback</param>
        /// <param name="connectInterval">How often to call the onConnect action (default is 1 hour)</param>
        public WebSocketWrapper(string uri, Action<string, WebSocketWrapper> onMessage, TimeSpan? keepAlive = null,
            Action<WebSocketWrapper> onConnect = null, Action<WebSocketWrapper> onDisconnect = null,
            TimeSpan? connectInterval = null)
        {
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = (keepAlive ?? TimeSpan.FromSeconds(30.0));
            _uri = new Uri(uri);
            _cancellationToken = _cancellationTokenSource.Token;
            _onMessage = onMessage;
            _onConnected = onConnect;
            _onDisconnected = onDisconnect;
            _connectInterval = (connectInterval ?? TimeSpan.FromHours(1.0));

            Task.Factory.StartNew(MessageWorkerThread);
            Task.Factory.StartNew(ListenWorkerThread);
        }

        /// <summary>
        /// Close and dispose of all resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", CancellationToken.None).Wait();
            }
            catch
            {
            }
            _disposed = true;
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
            ArraySegment<byte> messageArraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            await _ws.SendAsync(messageArraySegment, WebSocketMessageType.Text, true, _cancellationToken);
        }

        private void QueueAction(System.Action<WebSocketWrapper> action)
        {
            if (action != null)
            {
                _messageQueue.Add((System.Action)(() =>
                {
                    try
                    {
                        action(this);
                    }
                    catch
                    {
                    }
                }));
            }
        }

        private void QueueActionWithNoExceptions(System.Action<WebSocketWrapper> action)
        {
            if (action != null)
            {
                _messageQueue.Add((System.Action)(() =>
                {
                    while (true)
                    {
                        try
                        {
                            action.Invoke(this);
                            break;
                        }
                        catch
                        {
                        }
                    }
                }));
            }
        }

        private void ListenWorkerThread()
        {
            ArraySegment<byte> receiveBuffer = new ArraySegment<byte>(new byte[receiveChunkSize]);
            bool wasClosed = true;
            TimeSpan keepAlive = _ws.Options.KeepAliveInterval;
            MemoryStream stream = new MemoryStream();

            while (!_disposed)
            {
                try
                {
                    if (wasClosed)
                    {
                        // re-open the socket
                        wasClosed = false;
                        _ws = new ClientWebSocket();
                        _ws.ConnectAsync(_uri, CancellationToken.None).Wait();
                        QueueActionWithNoExceptions(_onConnected);
                    }

                    while (_ws.State == WebSocketState.Open)
                    {
                        Task<WebSocketReceiveResult> result;
                        do
                        {
                            result = _ws.ReceiveAsync(receiveBuffer, _cancellationToken);
                            result.Wait();
                            if (result.Result.MessageType == WebSocketMessageType.Close)
                            {
                                _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).Wait();
                                QueueAction(_onDisconnected);
                            }
                            else
                            {
                                stream.Write(receiveBuffer.Array, 0, result.Result.Count);
                            }

                        }
                        while (!result.Result.EndOfMessage);
                        if (stream.Length != 0)
                        {
                            string messageString = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                            stream.SetLength(0);
                            _messageQueue.Add(messageString);
                        }
                    }
                }
                catch
                {
                    QueueAction(_onDisconnected);
                    if (!_disposed)
                    {
                        // wait one half second before attempting reconnect
                        Task.Delay(500).Wait();
                    }
                }
                finally
                {
                    wasClosed = true;
                    try
                    {
                        _ws.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void MessageWorkerThread()
        {
            DateTime lastCheck = DateTime.UtcNow;

            while (!_disposed)
            {
                if (_messageQueue.TryTake(out object message, 100))
                {
                    try
                    {
                        if (message is System.Action action)
                        {
                            action();
                        }
                        else if (message is string messageString)
                        {
                            _onMessage?.Invoke(messageString, this);
                        }
                    }
                    catch
                    {
                    }
                }
                if (_connectInterval.Ticks > 0 && (DateTime.UtcNow - lastCheck) >= _connectInterval)
                {
                    lastCheck = DateTime.UtcNow;
                    QueueActionWithNoExceptions(_onConnected);
                }
            }
        }
    }
}