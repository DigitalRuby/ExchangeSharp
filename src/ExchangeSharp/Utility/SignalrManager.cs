/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#nullable enable
#define HAS_SIGNALR

#if HAS_SIGNALR

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Http;
using Microsoft.AspNet.SignalR.Client.Transports;
using Microsoft.AspNet.SignalR.Client.Infrastructure;

namespace ExchangeSharp
{
    /// <summary>
    /// Manages a signalr connection and web sockets
    /// </summary>
    public class SignalrManager
    {
        /// <summary>
        /// A connection to a specific end point in the hub
        /// </summary>
        public sealed class SignalrSocketConnection : IWebSocket
        {
            private readonly SignalrManager manager;
            private Func<string, Task> callback;
            private string functionFullName;
            private bool disposed;
            private bool initialConnectFired;

			private TimeSpan _connectInterval = TimeSpan.FromHours(1.0);
			/// <summary>
			/// Interval to call connect at regularly (default is 1 hour)
			/// </summary>
			public TimeSpan ConnectInterval
			{
				get { return _connectInterval; }
				set
				{
					_connectInterval = value;
					manager.ConnectInterval = value;
				}
			}

			private TimeSpan _keepAlive = TimeSpan.FromSeconds(5.0);
			/// <summary>
			/// Keep alive interval (default is 5 seconds)
			/// </summary>
			public TimeSpan KeepAlive
			{
				get { return _keepAlive; }
				set
				{
					_keepAlive = value;
					manager.KeepAlive = value;
				}
			}

			/// <summary>
			/// Connected event
			/// </summary>
			public event WebSocketConnectionDelegate Connected;

            /// <summary>
            /// Disconnected event
            /// </summary>
            public event WebSocketConnectionDelegate Disconnected;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="manager">Manager</param>
            public SignalrSocketConnection(SignalrManager manager)
            {
                this.manager = manager;
            }

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="functionName">Function name</param>
            /// <param name="callback">Callback for data</param>
            /// <param name="delayMilliseconds">Delay after invoking each object[] in param, used if the server will disconnect you for too many invoke too fast</param>
            /// <param name="param">End point parameters, each array of strings is a separate call to the end point function. For no parameters, pass null.</param>
            /// <returns>Connection</returns>
            public async Task OpenAsync(string functionName, Func<string, Task> callback, int delayMilliseconds = 0, object[][]? param = null)
            {
                callback.ThrowIfNull(nameof(callback), "Callback must not be null");

                SignalrManager _manager = this.manager;
                _manager.ThrowIfNull(nameof(manager), "Manager is null");

                Exception? ex = null;
                param = (param ?? new object[][] { new object[0] });
                string functionFullName = _manager.GetFunctionFullName(functionName);
                this.functionFullName = functionFullName;

                while (!disposed && !_manager.disposed)
                {
                    try
                    {
                        // performs any needed reconnect
                        await _manager.AddListener(functionName, callback, param);

                        while (!disposed && !_manager.disposed && _manager.hubConnection.State != ConnectionState.Connected)
                        {
                            await Task.Delay(100);
                        }

                        // ask for proxy after adding the listener, as the listener will force a connection if needed
                        IHubProxy _proxy = _manager.hubProxy;
                        if (_proxy == null)
                        {
                            throw new ArgumentNullException("Hub proxy is null");
                        }

                        // all parameters must succeed or we will give up and try the loop all over again
                        for (int i = 0; i < param.Length; i++)
                        {
                            if (i != 0)
                            {
                                await Task.Delay(delayMilliseconds);
                            }
                            if (!(await _proxy.Invoke<bool>(functionFullName, param[i])))
                            {
                                throw new APIException("Invoke returned success code of false");
                            }
                        }
                        ex = null;
                        break;
                    }
                    catch (Exception _ex)
                    {
                        // fail, remove listener
                        _manager.RemoveListener(functionName, callback);
                        ex = _ex;
                        Logger.Info("Error invoking hub proxy {0}: {1}", functionFullName, ex);
                        if (disposed || manager.disposed)
                        {
                            // give up, if we or the manager is disposed we are done
                            break;
                        }
                        else
                        {
                            // try again in a bit...
                            await Task.Delay(500);
                        }
                    }
                }

                if (ex == null && !disposed && !_manager.disposed)
                {
                    this.callback = callback;
                    lock (_manager.sockets)
                    {
                        _manager.sockets.Add(this);
                    }
                    if (!initialConnectFired)
                    {
                        initialConnectFired = true;

                        // kick off a connect event if this is the first time, the connect event can only get set after the open request is sent
                        Task.Run(async () =>
                        {
                            await Task.Delay(1000); // give time for the caller to set a connected event
                            await InvokeConnected();
                        }).ConfigureAwait(false).GetAwaiter();
                    }
                }
            }

            internal async Task InvokeConnected()
            {
                var connected = Connected;
                if (connected != null)
                {
                    await connected.Invoke(this);
                }
            }

            internal async Task InvokeDisconnected()
            {
                var disconnected = Disconnected;
                if (disconnected != null)
                {
                    await disconnected.Invoke(this);
                }
            }

            /// <summary>
            /// Dispose of the socket and remove from listeners
            /// </summary>
            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }
                disposed = true;

                try
                {
                    lock (manager.sockets)
                    {
                        manager.sockets.Remove(this);
                    }
                    manager.RemoveListener(functionFullName, callback);
                    InvokeDisconnected().GetAwaiter();
                }
                catch
                {
                }
            }

            /// <summary>
            /// Not supported
            /// </summary>
            /// <param name="message">Not supported</param>
            /// <returns>Not supported</returns>
            public Task<bool> SendMessageAsync(object message)
            {
                throw new NotSupportedException();
            }
        }

        public sealed class WebsocketCustomTransport : ClientTransportBase
        {
            private IConnection connection;
            private string connectionData;
			private readonly TimeSpan connectInterval;
			private readonly TimeSpan keepAlive;

			public ExchangeSharp.ClientWebSocket WebSocket { get; private set; }

            public override bool SupportsKeepAlive => true;

            public WebsocketCustomTransport(IHttpClient client, TimeSpan connectInterval, TimeSpan keepAlive) 
				: base(client, "webSockets")
            {
                WebSocket = new ExchangeSharp.ClientWebSocket();
                this.connectInterval = connectInterval;
				this.keepAlive = keepAlive;
            }

            ~WebsocketCustomTransport()
            {
                Dispose(false);
            }

            protected override void OnStart(IConnection con, string conData, CancellationToken disconToken)
            {
                connection = con;
                connectionData = conData;

                var connectUrl = UrlBuilder.BuildConnect(connection, Name, connectionData);

                // SignalR uses https, websocket4net uses wss
                connectUrl = connectUrl.Replace("http://", "ws://").Replace("https://", "wss://");

                IDictionary<string, string> cookies = new Dictionary<string, string>();
                if (connection.CookieContainer != null)
                {
                    var container = connection.CookieContainer.GetCookies(new Uri(connection.Url));
                    foreach (Cookie cookie in container)
                    {
                        cookies.Add(cookie.Name, cookie.Value);
                    }
                }

                WebSocket.Uri = new Uri(connectUrl);
                WebSocket.OnBinaryMessage = WebSocketOnBinaryMessageReceived;
                WebSocket.OnTextMessage = WebSocketOnTextMessageReceived;
				WebSocket.ConnectInterval = connectInterval;
                WebSocket.KeepAlive = keepAlive;         
                WebSocket.Start();
            }

            protected override void OnStartFailed()
            {
                Dispose();
            }

            public override async Task Send(IConnection con, string data, string conData)
            {
                await WebSocket.SendMessageAsync(data);
            }

            public override void LostConnection(IConnection con)
            {
                connection.Stop();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (WebSocket != null)
                    {
                        DisposeWebSocket();
                    }
                }

                base.Dispose(disposing);
            }

            private void DisposeWebSocket()
            {
                WebSocket.Dispose();
            }

            /*
            private void WebSocketOnClosed()
            {
                connection.Stop();
            }

            private void WebSocketOnError(Exception e)
            {
                connection.OnError(e);
            }
            */

            private Task WebSocketOnBinaryMessageReceived(IWebSocket socket, byte[] data)
            {
                string dataText = data.ToStringFromUTF8();
                ProcessResponse(connection, dataText);
                return Task.CompletedTask;
            }

            private Task WebSocketOnTextMessageReceived(IWebSocket socket, string data)
            {
                ProcessResponse(connection, data);
                return Task.CompletedTask;
            }
        }

        private class HubListener
        {
            public List<Func<string, Task>> Callbacks { get; } = new List<Func<string, Task>>();
            public string FunctionName { get; set; }
            public string FunctionFullName { get; set; }
            public object[][] Param { get; set; }
        }

        private readonly Dictionary<string, HubListener> listeners = new Dictionary<string, HubListener>();
        private readonly List<SignalrSocketConnection> sockets = new List<SignalrSocketConnection>();
        private readonly SemaphoreSlim reconnectLock = new SemaphoreSlim(1);

		private WebsocketCustomTransport? customTransport;
		private HubConnection? hubConnection;
        private IHubProxy? hubProxy;
        private bool disposed;

		private TimeSpan _connectInterval = TimeSpan.FromHours(1.0);
		/// <summary>
		/// Interval to call connect at regularly (default is 1 hour)
		/// </summary>
		public TimeSpan ConnectInterval
		{
			get { return _connectInterval; }
			set
			{
				_connectInterval = value;
				if (customTransport != null)
					customTransport.WebSocket.ConnectInterval = value;
			}
		}

		private TimeSpan _keepAlive = TimeSpan.FromSeconds(5.0);
		/// <summary>
		/// Keep alive interval (default is 5 seconds)
		/// </summary>
		public TimeSpan KeepAlive
		{
			get { return _keepAlive; }
			set
			{
				_keepAlive = value;
				if (customTransport != null)
					customTransport.WebSocket.KeepAlive = value;
			}
		}

		/// <summary>
		/// Connection url
		/// </summary>
		public string ConnectionUrl { get; private set; }

        /// <summary>
        /// Hub name
        /// </summary>
        public string HubName { get; set; }

        /// <summary>
        /// Function names to full function names - populate before calling start
        /// </summary>
        public Dictionary<string, string> FunctionNamesToFullNames { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal string GetFunctionFullName(string functionName)
        {
            if (!FunctionNamesToFullNames.TryGetValue(functionName, out string fullFunctionName))
            {
                return functionName;
            }
            return fullFunctionName;
        }

        private async Task AddListener(string functionName, Func<string, Task> callback, object[][] param)
        {
            string functionFullName = GetFunctionFullName(functionName);

            // ensure connected before adding the listener
            await EnsureConnected(() =>
            {
                lock (listeners)
                {
                    if (!listeners.TryGetValue(functionFullName, out HubListener listener))
                    {
                        listeners[functionFullName] = listener = new HubListener { FunctionName = functionName, FunctionFullName = functionFullName, Param = param };
                    }
                    if (!listener.Callbacks.Contains(callback))
                    {
                        listener.Callbacks.Add(callback);
                    }
                }
            });
        }

        private void RemoveListener(string functionName, Func<string, Task> callback)
        {
            lock (listeners)
            {
                string functionFullName = GetFunctionFullName(functionName);
                if (listeners.TryGetValue(functionFullName, out HubListener listener))
                {
                    listener.Callbacks.Remove(callback);
                    if (listener.Callbacks.Count == 0)
                    {
                        listeners.Remove(functionFullName);
                    }
                }
                if (listeners.Count == 0)
                {
                    Stop();
                }
            }
        }

        private async Task HandleResponse(string functionName, string data)
        {
            string functionFullName = GetFunctionFullName(functionName);
            data = Decode(data);
            Func<string, Task>[] actions = null;

            lock (listeners)
            {
                if (listeners.TryGetValue(functionFullName, out HubListener listener))
                {
                    actions = listener.Callbacks.ToArray();
                }
            }

            if (actions != null)
            {
                foreach (Func<string, Task> func in actions)
                {
                    try
                    {
                        await func(data);
                    }
                    catch (Exception ex)
                    {
                        Logger.Info(ex.ToString());
                    }
                }
            }
        }

        private void SocketClosed()
        {
            if (listeners.Count == 0)
            {
                return;
            }
            Task.Run(() => EnsureConnected(null));
        }

        private async Task EnsureConnected(Action connectCallback)
        {
            if (!(await reconnectLock.WaitAsync(0)))
            {
                return;
            }
            try
            {
                // if hubConnection is null, exception will throw out
                while (!disposed && (hubConnection == null || (hubConnection.State != ConnectionState.Connected && hubConnection.State != ConnectionState.Connecting)))
                {
                    try
                    {
                        await StartAsync();
                        connectCallback?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug(ex.ToString());

                        // wait 5 seconds before attempting reconnect
                        for (int i = 0; i < 50 && !disposed; i++)
                        {
                            await Task.Delay(100);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info(ex.ToString());
            }
            finally
            {
                reconnectLock.Release();
            }
        }

        /// <summary>
        /// Constructor - derived class should populate FunctionNamesToFullNames before starting
        /// </summary>
        /// <param name="connectionUrl">Connection url</param>
        /// <param name="hubName">Hub name</param>
        public SignalrManager(string connectionUrl, string hubName)
        {
            ConnectionUrl = connectionUrl;
            HubName = hubName;
        }

        /// <summary>
        /// Start the hub connection - populate FunctionNamesToFullNames first
        /// </summary>
        /// <returns></returns>
        public async Task StartAsync()
        {
            // stop any previous hub connection
            hubConnection?.Stop();
            hubConnection?.Dispose();

            // make a new hub connection
            hubConnection = new HubConnection(ConnectionUrl, false);
            hubConnection.Closed += SocketClosed;

#if DEBUG

            //hubConnection.TraceLevel = TraceLevels.All;
            //hubConnection.TraceWriter = Console.Out;

#endif

            hubProxy = hubConnection.CreateHubProxy(HubName);
            if (hubProxy == null)
            {
                throw new APIException("CreateHubProxy - proxy is null, this should never happen");
            }

            // assign callbacks for events
            foreach (string key in FunctionNamesToFullNames.Keys)
            {
                hubProxy.On(key, async (string data) => await HandleResponse(key, data));
            }

            // create a custom transport, the default transport is really buggy
            DefaultHttpClient client = new DefaultHttpClient();
            customTransport = new WebsocketCustomTransport(client, ConnectInterval, KeepAlive);
            var autoTransport = new AutoTransport(client, new IClientTransport[] { customTransport });
            hubConnection.TransportConnectTimeout = hubConnection.DeadlockErrorTimeout = TimeSpan.FromSeconds(10.0);

            // setup connect event
            customTransport.WebSocket.Connected += async (ws) =>
            {
                try
                {
                    SignalrSocketConnection[] socketsCopy;
                    lock (sockets)
                    {
                        socketsCopy = sockets.ToArray();
                    }
                    foreach (SignalrSocketConnection socket in socketsCopy)
                    {
                        await socket.InvokeConnected();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info(ex.ToString());
                }
            };

            // setup disconnect event
            customTransport.WebSocket.Disconnected += async (ws) =>
            {
                try
                {
                    SignalrSocketConnection[] socketsCopy;
                    lock (sockets)
                    {
                        socketsCopy = sockets.ToArray();
                    }
                    foreach (SignalrSocketConnection socket in socketsCopy)
                    {
                        await socket.InvokeDisconnected();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Info(ex.ToString());
                }
                try
                {
                    // tear down the hub connection, we must re-create it whenever a web socket disconnects
                    hubConnection?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Info(ex.ToString());
                }
            };

            try
            {
                // it's possible for the hub connection to disconnect during this code if connection is crappy
                // so we simply catch the exception and log an info message, the disconnect/reconnect loop will
                // catch the close and re-initiate this whole method again
                await hubConnection.Start(autoTransport);

                // get list of listeners quickly to limit lock
                HubListener[] listeners;
                lock (this.listeners)
                {
                    listeners = this.listeners.Values.ToArray();
                }

                // re-call the end point to enable messages
                foreach (var listener in listeners)
                {
                    foreach (object[] p in listener.Param)
                    {
                        await hubProxy.Invoke<bool>(listener.FunctionFullName, p);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Info(ex.ToString());
            }
        }

        /// <summary>
        /// Stop the hub connection
        /// </summary>
        public void Stop()
        {
			try
			{
				hubConnection.Stop(TimeSpan.FromSeconds(0.1));
			}
			catch (NullReferenceException) 
			{ // bug in SignalR where Stop() throws a NRE if it times out
			  // https://github.com/SignalR/SignalR/issues/3561
			}
		}

        /// <summary>
        /// Finalizer
        /// </summary>
        ~SignalrManager()
        {
            Dispose();
        }

        /// <summary>
        /// Dispose of the hub connection
        /// </summary>
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            try
            {
                hubConnection.Transport?.Dispose();
                hubConnection.Dispose();
            }
            catch
            {
                // eat exceptions here, we don't care if it fails
            }
            hubConnection = null;
        }

        /// <summary>
        /// Get auth context
        /// </summary>
        /// <param name="apiKey">API key</param>
        /// <returns>String</returns>
        public async Task<string> GetAuthContext(string apiKey) => await hubProxy.Invoke<string>("GetAuthContext", apiKey);

        /// <summary>
        /// Authenticate
        /// </summary>
        /// <param name="apiKey">API key</param>
        /// <param name="signedChallenge">Challenge</param>
        /// <returns>Result</returns>
        public async Task<bool> Authenticate(string apiKey, string signedChallenge) => await hubProxy.Invoke<bool>("Authenticate", apiKey, signedChallenge);

        /// <summary>
        /// Converts CoreHub2 socket wire protocol data into JSON.
        /// Data goes from base64 encoded to gzip (byte[]) to minifed JSON.
        /// </summary>
        /// <param name="wireData">Wire data</param>
        /// <returns>JSON</returns>
        public static string Decode(string wireData)
        {
            // Step 1: Base64 decode the wire data into a gzip blob
            byte[] gzipData = Convert.FromBase64String(wireData);

            // Step 2: Decompress gzip blob into minified JSON
            using (var decompressedStream = new MemoryStream())
            using (var compressedStream = new MemoryStream(gzipData))
            using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(decompressedStream);
                decompressedStream.Position = 0;

                using (var streamReader = new StreamReader(decompressedStream))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }

        /// <summary>
        /// Create signature
        /// </summary>
        /// <param name="apiSecret">API secret</param>
        /// <param name="challenge">Challenge</param>
        /// <returns>Signature</returns>
        public static string CreateSignature(string apiSecret, string challenge)
        {
            // Get hash by using apiSecret as key, and challenge as data
            var hmacSha512 = new HMACSHA512(apiSecret.ToBytesUTF8());
            var hash = hmacSha512.ComputeHash(challenge.ToBytesUTF8());
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }
    }
}

#endif
