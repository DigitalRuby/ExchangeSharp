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
    /// Wraps a web socket for easy dispose later, along with auto-reconnect and message and reader queues
    /// </summary>
    public sealed class WebSocketWrapper : IWebSocket
    {
        private const int receiveChunkSize = 8192;

        private ClientWebSocket webSocket = new ClientWebSocket();
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;
        private readonly BlockingCollection<object> messageQueue = new BlockingCollection<object>(new ConcurrentQueue<object>());

        private bool disposed;

        /// <summary>
        /// The uri to connect to
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Action to handle incoming messages
        /// </summary>
        public Action<byte[], WebSocketWrapper> OnMessage { get; set; }

        /// <summary>
        /// Interval to call connect at regularly (default is 1 hour)
        /// </summary>
        public TimeSpan ConnectInterval { get; set; } = TimeSpan.FromHours(1.0);

        /// <summary>
        /// Keep alive interval (default is 30 seconds)
        /// </summary>
        public TimeSpan KeepAlive { get; set; } = TimeSpan.FromSeconds(30.0);

        /// <summary>
        /// Allows additional listeners for connect event
        /// </summary>
        public event Action<IWebSocket> Connected;

        /// <summary>
        /// Allows additional listeners for disconnect event
        /// </summary>
        public event Action<IWebSocket> Disconnected;

        /// <summary>
        /// Whether to close the connection gracefully, this can cause the close to take longer.
        /// </summary
        public bool CloseCleanly { get; set; }

        /// <summary>
        /// Default constructor, does not begin listening immediately. You must set the properties and then call Start.
        /// </summary>
        public WebSocketWrapper()
        {
            cancellationToken = cancellationTokenSource.Token;
        }

        /// <summary>
        /// Start the web socket listening and processing
        /// </summary>
        public void Start()
        {
            // kick off message parser and message listener
            Task.Run((Action)MessageWorkerThread);
            Task.Run(ListenWorkerThread);
        }

        /// <summary>
        /// Close and dispose of all resources, stops the web socket
        /// </summary>
        public void Dispose()
        {
            disposed = true;
            try
            {
                if (CloseCleanly)
                {
                    webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Dispose", cancellationToken).GetAwaiter().GetResult();
                }
                else
                {
                    webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Dispose", cancellationToken).GetAwaiter().GetResult();
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

        private void QueueActions(params Action<WebSocketWrapper>[] actions)
        {
            if (actions != null && actions.Length != 0)
            {
                messageQueue.Add((Action)(() =>
                {
                    foreach (var action in actions)
                    {
                        try
                        {
                            action?.Invoke(this);
                        }
                        catch
                        {
                        }
                    }
                }));
            }
        }

        private void QueueActionsWithNoExceptions(params Action<WebSocketWrapper>[] actions)
        {
            if (actions != null && actions.Length != 0)
            {
                messageQueue.Add((Action)(() =>
                {
                    foreach (var action in actions)
                    {
                        while (true)
                        {
                            try
                            {
                                action?.Invoke(this);
                                break;
                            }
                            catch
                            {
                            }
                        }
                    }
                }));
            }
        }

        private async Task ListenWorkerThread()
        {
            ArraySegment<byte> receiveBuffer = new ArraySegment<byte>(new byte[receiveChunkSize]);
            TimeSpan keepAlive = webSocket.Options.KeepAliveInterval;
            MemoryStream stream = new MemoryStream();
            WebSocketReceiveResult result;
            bool wasConnected = false;

            while (!disposed)
            {
                try
                {
                    // open the socket
                    webSocket.Options.KeepAliveInterval = KeepAlive;
                    wasConnected = false;
                    await webSocket.ConnectAsync(Uri, cancellationToken);
                    wasConnected = true;

                    // on connect may make additional calls that must succeed, such as rest calls
                    // for lists, etc.
                    QueueActionsWithNoExceptions(Connected);

                    while (webSocket.State == WebSocketState.Open)
                    {
                        do
                        {
                            result = await webSocket.ReceiveAsync(receiveBuffer, cancellationToken);
                            if (result.MessageType == WebSocketMessageType.Close)
                            {
                                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken);
                                QueueActions(Disconnected);
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
                    // eat exceptions, most likely a result of a disconnect, either way we will re-create the web socket
                }

                if (wasConnected)
                {
                    QueueActions(Disconnected);
                }
                try
                {
                    webSocket.Dispose();
                }
                catch
                {
                }
                if (!disposed)
                {
                    // wait 5 seconds before attempting reconnect
                    webSocket = new ClientWebSocket();
                    await Task.Delay(5000);
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
                            OnMessage?.Invoke(messageBytes, this);
                        }
                    }
                    catch
                    {
                    }
                }
                if (ConnectInterval.Ticks > 0 && (DateTime.UtcNow - lastCheck) >= ConnectInterval)
                {
                    lastCheck = DateTime.UtcNow;

                    // this must succeed, the callback may be requests lists or other resources that must not fail
                    QueueActionsWithNoExceptions(Connected);
                }
            }
        }
    }
}
