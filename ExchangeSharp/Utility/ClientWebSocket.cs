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
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Wraps a web socket for easy dispose later, along with auto-reconnect and message and reader queues
    /// </summary>
    public sealed class ClientWebSocket : IWebSocket
    {
        /// <summary>
        /// Client web socket implementation
        /// </summary>
        public interface IClientWebSocketImplementation : IDisposable
        {
            /// <summary>
            /// Web socket state
            /// </summary>
            WebSocketState State { get; }

            /// <summary>
            /// Keep alive interval (heartbeat)
            /// </summary>
            TimeSpan KeepAliveInterval { get; set; }

            /// <summary>
            /// Close cleanly
            /// </summary>
            /// <param name="closeStatus"></param>
            /// <param name="statusDescription"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);

            /// <summary>
            /// Close output immediately
            /// </summary>
            /// <param name="closeStatus"></param>
            /// <param name="statusDescription"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);

            /// <summary>
            /// Connect
            /// </summary>
            /// <param name="uri"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

            /// <summary>
            /// Receive
            /// </summary>
            /// <param name="buffer"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);

            /// <summary>
            /// Send
            /// </summary>
            /// <param name="buffer"></param>
            /// <param name="messageType"></param>
            /// <param name="endOfMessage"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken);
        }

        private class ClientWebSocketImplementation : IClientWebSocketImplementation
        {
            private readonly System.Net.WebSockets.ClientWebSocket webSocket = new System.Net.WebSockets.ClientWebSocket();

            public WebSocketState State
            {
                get { return webSocket.State; }
            }

            public TimeSpan KeepAliveInterval
            {
                get { return webSocket.Options.KeepAliveInterval; }
                set { webSocket.Options.KeepAliveInterval = value; }
            }

            public void Dispose()
            {
                webSocket.Dispose();
            }

            public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            {
                return webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
            }

            public Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
            {
                return webSocket.CloseOutputAsync(closeStatus, statusDescription, cancellationToken);
            }

            public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
            {
                return webSocket.ConnectAsync(uri, cancellationToken);
            }

            public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
            {
                return webSocket.ReceiveAsync(buffer, cancellationToken);
            }

            public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
            {
                return webSocket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
            }
        }

        private const int receiveChunkSize = 8192;

        private static Func<IClientWebSocketImplementation> webSocketCreator = () => new ClientWebSocketImplementation();

        // created from factory, allows swapping out underlying implementation
        private IClientWebSocketImplementation webSocket;

        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly CancellationToken cancellationToken;
        private readonly BlockingCollection<object> messageQueue = new BlockingCollection<object>(new ConcurrentQueue<object>());

        private bool disposed;

        private void CreateWebSocket()
        {
            webSocket = webSocketCreator();
        }

        /// <summary>
        /// The uri to connect to
        /// </summary>
        public Uri Uri { get; set; }

        /// <summary>
        /// Action to handle incoming messages
        /// </summary>
        public Func<IWebSocket, byte[], Task> OnMessage { get; set; }

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
        public event Func<IWebSocket, Task> Connected;

        /// <summary>
        /// Allows additional listeners for disconnect event
        /// </summary>
        public event Func<IWebSocket, Task> Disconnected;

        /// <summary>
        /// Whether to close the connection gracefully, this can cause the close to take longer.
        /// </summary
        public bool CloseCleanly { get; set; }

        /// <summary>
        /// Register a function that will be responsible for creating the underlying web socket implementation
        /// By default, C# built-in web sockets are used (Windows 8.1+ required). But you could swap out
        /// a different web socket for other platforms, testing, or other specialized needs.
        /// </summary>
        /// <param name="creator">Creator function. Pass null to go back to the default implementation.</param>
        public static void RegisterWebSocketCreator(Func<IClientWebSocketImplementation> creator)
        {
            if (creator == null)
            {
                webSocketCreator = () => new ClientWebSocketImplementation();
            }
            else
            {
                webSocketCreator = creator;
            }
        }

        /// <summary>
        /// Default constructor, does not begin listening immediately. You must set the properties and then call Start.
        /// </summary>
        public ClientWebSocket()
        {
            cancellationToken = cancellationTokenSource.Token;
        }

        /// <summary>
        /// Start the web socket listening and processing
        /// </summary>
        public void Start()
        {
            CreateWebSocket();

            // kick off message parser and message listener
            Task.Run(MessageTask);
            Task.Run(ReadTask);
        }

        /// <summary>
        /// Close and dispose of all resources, stops the web socket and shuts it down.
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
        /// <returns>True if success, false if error</returns>
        public bool SendMessage(string message)
        {
            return SendMessageAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        /// ASYNC - send a message to the WebSocket server.
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>True if success, false if error</returns>
        public async Task<bool> SendMessageAsync(string message)
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    ArraySegment<byte> messageArraySegment = new ArraySegment<byte>(message.ToBytesUTF8());
                    await webSocket.SendAsync(messageArraySegment, WebSocketMessageType.Text, true, cancellationToken);
                    return true;
                }
            }
            catch
            {
                // don't care if this fails, maybe the socket is in process of dispose, who knows...
            }
            return false;
        }

        private void QueueActions(params Func<IWebSocket, Task>[] actions)
        {
            if (actions != null && actions.Length != 0)
            {
                messageQueue.Add((Func<Task>)(async () =>
                {
                    foreach (var action in actions.Where(a => a != null))
                    {
                        try
                        {
                            await action.Invoke(this);
                        }
                        catch
                        {
                        }
                    }
                }));
            }
        }

        private void QueueActionsWithNoExceptions(params Func<IWebSocket, Task>[] actions)
        {
            if (actions != null && actions.Length != 0)
            {
                messageQueue.Add((Func<Task>)(async () =>
                {
                    foreach (var action in actions.Where(a => a != null))
                    {
                        while (true)
                        {
                            try
                            {
                                await action.Invoke(this);
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

        private async Task ReadTask()
        {
            ArraySegment<byte> receiveBuffer = new ArraySegment<byte>(new byte[receiveChunkSize]);
            TimeSpan keepAlive = webSocket.KeepAliveInterval;
            MemoryStream stream = new MemoryStream();
            WebSocketReceiveResult result;
            bool wasConnected = false;

            while (!disposed)
            {
                try
                {
                    // open the socket
                    webSocket.KeepAliveInterval = KeepAlive;
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
                    CreateWebSocket();
                    await Task.Delay(5000);
                }
            }
        }

        private async Task MessageTask()
        {
            DateTime lastCheck = DateTime.UtcNow;

            while (!disposed)
            {
                if (messageQueue.TryTake(out object message, 100))
                {
                    try
                    {
                        if (message is Func<Task> action)
                        {
                            await action();
                        }
                        else if (message is byte[] messageBytes)
                        {
                            await OnMessage?.Invoke(this, messageBytes);
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

    /// <summary>
    /// Web socket interface
    /// </summary>
    public interface IWebSocket : IDisposable
    {
        /// <summary>
        /// Connected event
        /// </summary>
        event Func<IWebSocket, Task> Connected;

        /// <summary>
        /// Disconnected event
        /// </summary>
        event Func<IWebSocket, Task> Disconnected;

        /// <summary>
        /// Send a message over the web socket
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>True if success, false if error</returns>
        bool SendMessage(string message);

        /// <summary>
        /// ASYNC - Send a message over the web socket
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <returns>True if success, false if error</returns>
        Task<bool> SendMessageAsync(string message);
    }
}
