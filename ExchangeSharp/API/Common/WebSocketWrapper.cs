/*
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
    /// <summary>
    /// Wraps a web socket for easy dispose later
    /// </summary>
    public sealed class WebSocketWrapper : IDisposable
    {
        private const int receiveChunkSize = 8192;

        private ClientWebSocket webSocket = new ClientWebSocket();
        private readonly Uri uri;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;
        private readonly BlockingCollection<object> messageQueue = new BlockingCollection<object>(new ConcurrentQueue<object>());
        private readonly TimeSpan keepAlive;
        private readonly Action<byte[], WebSocketWrapper> onMessage;
        private readonly Action<WebSocketWrapper> onConnected;
        private readonly Action<WebSocketWrapper> onDisconnected;
        private readonly TimeSpan connectInterval;

        private bool disposed;
        
        /// <summary>
        /// Whether to close the connection gracefully, this can cause the close to take longer.
        /// </summary
        public bool CloseCleanly { get; set; }

        /// <summary>
        /// Constructor, also begins listening and processing messages immediately
        /// </summary>
        /// <param name="uri">Uri to connect to</param>
        /// <param name="onMessage">Message callback</param>
        /// <param name="onConnect">Connect callback, will get called on connection and every connectInterval (default 1 hour). This is a great place
        /// to do setup, such as creating lookup dictionaries, etc. This method will re-execute until it executes without exceptions thrown.</param>
        /// <param name="onDisconnect">Disconnect callback</param>
        /// <param name="keepAlive">Keep alive time, default is 30 seconds</param>
        /// <param name="connectInterval">How often to call the onConnect action (default is 1 hour)</param>
        public WebSocketWrapper
        (
            string uri,
            Action<byte[], WebSocketWrapper> onMessage,
            Action<WebSocketWrapper> onConnect = null,
            Action<WebSocketWrapper> onDisconnect = null,
            TimeSpan? keepAlive = null,
            TimeSpan? connectInterval = null
        )
        {
            this.uri = new Uri(uri);
            this.onMessage = onMessage;
            this.onConnected = onConnect;
            this.onDisconnected = onDisconnect;
            this.keepAlive = (keepAlive ?? TimeSpan.FromSeconds(30.0));
            this.connectInterval = (connectInterval ?? TimeSpan.FromHours(1.0));
            cancellationToken = cancellationTokenSource.Token;

            // kick off message parser and message listener
            Task.Run((Action)MessageWorkerThread);
            Task.Run(ListenWorkerThread);
        }

        /// <summary>
        /// Close and dispose of all resources
        /// </summary>
        public void Dispose()
        {
            disposed = true;
            try
            {
                if (CloseCleanly)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", CancellationToken.None).GetAwaiter().GetResult();
                }
                else
                {
                    webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Dispose", CancellationToken.None).GetAwaiter().GetResult();
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Send a message to the WebSocket server.
        /// </summary>
        /// <param name="message">The message to send</param>
        public void SendMessage(string message)
        {
            SendMessageAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task SendMessageAsync(string message)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    ArraySegment<byte> messageArraySegment = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                    await webSocket.SendAsync(messageArraySegment, WebSocketMessageType.Text, true, cancellationToken);
                }
            }
            catch
            {
                // don't care if this fails, maybe the socket is in process of dispose, who knows...
            }
        }

        private void QueueAction(Action<WebSocketWrapper> action)
        {
            if (action != null)
            {
                messageQueue.Add((Action)(() =>
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

        private void QueueActionWithNoExceptions(Action<WebSocketWrapper> action)
        {
            if (action != null)
            {
                messageQueue.Add((Action)(() =>
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

        private async Task ListenWorkerThread()
        {
            ArraySegment<byte> receiveBuffer = new ArraySegment<byte>(new byte[receiveChunkSize]);
            bool wasClosed = true;
            TimeSpan keepAlive = webSocket.Options.KeepAliveInterval;
            MemoryStream stream = new MemoryStream();
            WebSocketReceiveResult result;

            while (!disposed)
            {
                try
                {
                    if (wasClosed)
                    {
                        // re-open the socket
                        wasClosed = false;
                        webSocket.Options.KeepAliveInterval = this.keepAlive;
                        await webSocket.ConnectAsync(uri, cancellationToken);
                        QueueActionWithNoExceptions(onConnected);
                    }

                    while (webSocket.State == WebSocketState.Open)
                    {
                        do
                        {
                            result = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                                QueueAction(onDisconnected);
                            }
                            else
                            {
                                stream.Write(receiveBuffer.Array, 0, result.Count);
                            }

                        }
                        while (!result.EndOfMessage);
                        if (stream.Length != 0)
                        {
                            // make a copy of the bytes, the memory stream will be re-used and could potentially corrupt in multi-threaded environments
                            byte[] bytesCopy = new byte[stream.Length];
                            Array.Copy(stream.GetBuffer(), bytesCopy, stream.Length);
                            stream.SetLength(0);
                            messageQueue.Add(bytesCopy);
                        }
                    }
                }
                catch
                {
                    QueueAction(onDisconnected);
                    try
                    {
                        webSocket?.Dispose();
                    }
                    catch
                    {
                    }
                    webSocket = new ClientWebSocket();
                    if (!disposed)
                    {
                        // wait one second before attempting reconnect
                        await Task.Delay(1000);
                    }
                }
                finally
                {
                    wasClosed = true;
                }
            }
        }

        private void MessageWorkerThread()
        {
            DateTime lastCheck = DateTime.UtcNow;

            while (!disposed)
            {
                if (messageQueue.TryTake(out object message, 100))
                {
                    try
                    {
                        if (message is Action action)
                        {
                            action();
                        }
                        else if (message is byte[] messageBytes)
                        {
                            onMessage?.Invoke(messageBytes, this);
                        }
                    }
                    catch
                    {
                    }
                }
                if (connectInterval.Ticks > 0 && (DateTime.UtcNow - lastCheck) >= connectInterval)
                {
                    lastCheck = DateTime.UtcNow;
                    QueueActionWithNoExceptions(onConnected);
                }
            }
        }
    }
}
