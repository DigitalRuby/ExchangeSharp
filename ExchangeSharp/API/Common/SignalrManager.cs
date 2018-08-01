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
            private Action<string> callback;
            private string functionFullName;

            /// <summary>
            /// Connected event
            /// </summary>
            public event Action<IWebSocket> Connected;

            /// <summary>
            /// Disconnected event
            /// </summary>
            public event Action<IWebSocket> Disconnected;

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
            public async Task OpenAsync(string functionName, Action<string> callback, int delayMilliseconds = 0, object[][] param = null)
            {
                if (callback != null)
                {
                    param = (param ?? new object[][] { new object[0] });
                    manager.AddListener(functionName, callback, param);
                    string functionFullName = manager.GetFunctionFullName(functionName);
                    Exception ex = null;
                    try
                    {
                        for (int i = 0; i < param.Length; i++)
                        {
                            if (i != 0)
                            {
                                await Task.Delay(delayMilliseconds);
                            }
                            if (!(await manager.hubProxy.Invoke<bool>(functionFullName, param[i])))
                            {
                                throw new APIException("Invoke returned success code of false");
                            }
                        }
                    }
                    catch (Exception _ex)
                    {
                        ex = _ex;
                        Console.WriteLine("Error invoking hub proxy {0}: {1}", functionFullName, ex);
                    }
                    if (ex == null)
                    {
                        this.callback = callback;
                        this.functionFullName = functionFullName;
                        lock (manager.sockets)
                        {
                            manager.sockets.Add(this);
                        }
                        return;
                    }

                    // fail, remove listener
                    manager.RemoveListener(functionName, callback);
                    throw ex;
                }
                throw new ArgumentNullException(nameof(callback));
            }

            internal void InvokeConnected()
            {
                Connected?.Invoke(this);
            }

            internal void InvokeDisconnected()
            {
                Disconnected?.Invoke(this);
            }

            private void WebSocket_Connected(IWebSocket obj)
            {
                Connected?.Invoke(this);
            }

            private void WebSocket_Disconnected(IWebSocket obj)
            {
                Disconnected?.Invoke(this);
            }

            /// <summary>
            /// Dispose of the socket and remove from listeners
            /// </summary>
            public void Dispose()
            {
                try
                {
                    lock (manager.sockets)
                    {
                        manager.sockets.Remove(this);
                    }
                    manager.RemoveListener(functionFullName, callback);
                    Disconnected?.Invoke(this);
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
            public bool SendMessage(string message)
            {
                throw new NotSupportedException();
            }

            /// <summary>
            /// Not supported
            /// </summary>
            /// <param name="message">Not supported</param>
            /// <returns>Not supported</returns>
            public Task<bool> SendMessageAsync(string message)
            {
                throw new NotSupportedException();
            }
        }

        public sealed class WebsocketCustomTransport : ClientTransportBase
        {
            private IConnection connection;
            private string connectionData;
            public WebSocketWrapper WebSocket { get; private set; }

            public override bool SupportsKeepAlive => true;

            public WebsocketCustomTransport(IHttpClient client) : base(client, "webSockets")
            {
                WebSocket = new WebSocketWrapper();
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
                WebSocket.OnMessage = WebSocketOnMessageReceived;
                WebSocket.KeepAlive = TimeSpan.FromSeconds(5.0);
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
                WebSocket = null;
            }

            private void WebSocketOnClosed()
            {
                connection.Stop();
            }

            private void WebSocketOnError(Exception e)
            {
                connection.OnError(e);
            }

            private void WebSocketOnMessageReceived(byte[] data, WebSocketWrapper _webSocket)
            {
                string dataText = data.ToStringFromUTF8();
                ProcessResponse(connection, dataText);
            }
        }

        private class HubListener
        {
            public List<Action<string>> Callbacks { get; } = new List<Action<string>>();
            public string FunctionName { get; set; }
            public string FunctionFullName { get; set; }
            public object[][] Param { get; set; }
        }

        private readonly Dictionary<string, HubListener> listeners = new Dictionary<string, HubListener>();
        private readonly List<IWebSocket> sockets = new List<IWebSocket>();

        private HubConnection hubConnection;
        private IHubProxy hubProxy;
        private WebsocketCustomTransport customTransport;
        private bool reconnecting;
        private bool disposed;

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

        private void AddListener(string functionName, Action<string> callback, object[][] param)
        {
            string functionFullName = GetFunctionFullName(functionName);

            // ensure connected before adding the listener
            ReconnectLoop().ContinueWith((t) =>
            {
                lock (listeners)
                {
                    if (!listeners.TryGetValue(functionName, out HubListener listener))
                    {
                        listeners[functionFullName] = listener = new HubListener { FunctionName = functionName, FunctionFullName = functionFullName, Param = param };
                    }
                    if (!listener.Callbacks.Contains(callback))
                    {
                        listener.Callbacks.Add(callback);
                    }
                }
            }).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private void RemoveListener(string functionName, Action<string> callback)
        {
            lock (listeners)
            {
                string functionFullName = GetFunctionFullName(functionName);
                if (listeners.TryGetValue(functionFullName, out HubListener listener))
                {
                    listener.Callbacks.Remove(callback);
                    if (listener.Callbacks.Count == 0)
                    {
                        listeners.Remove(functionName);
                    }
                }
                if (listeners.Count == 0)
                {
                    Stop();
                }
            }
        }

        private void HandleResponse(string functionName, string data)
        {
            string functionFullName = GetFunctionFullName(functionName);
            data = Decode(data);
            Action<string>[] actions = null;

            lock (listeners)
            {
                if (listeners.TryGetValue(functionFullName, out HubListener listener))
                {
                    actions = listener.Callbacks.ToArray();
                }
            }

            if (actions != null)
            {
                Parallel.ForEach(actions, (callback) =>
                {
                    try
                    {
                        callback(data);
                    }
                    catch
                    {
                    }
                });
            }
        }

        private void SocketClosed()
        {
            if (listeners.Count == 0 || reconnecting)
            {
                return;
            }
            Task.Run(ReconnectLoop);
        }

        private async Task ReconnectLoop()
        {
            if (reconnecting)
            {
                return;
            }
            reconnecting = true;
            try
            {
                // if hubConnection is null, exception will throw out
                while (!disposed && (hubConnection == null || (hubConnection.State != ConnectionState.Connected && hubConnection.State != ConnectionState.Connecting)))
                {
                    try
                    {
                        await StartAsync();
                    }
                    catch
                    {
                        // wait 5 seconds before attempting reconnect
                        await Task.Delay(5000);
                    }
                }
            }
            catch
            {
            }
            reconnecting = false;
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

            hubConnection.TraceLevel = TraceLevels.All;
            hubConnection.TraceWriter = Console.Out;

#endif

            hubProxy = hubConnection.CreateHubProxy(HubName);

            // assign callbacks for events
            foreach (string key in FunctionNamesToFullNames.Keys)
            {
                hubProxy.On(key, (string data) => HandleResponse(key, data));
            }

            // create a custom transport, the default transport is really buggy
            DefaultHttpClient client = new DefaultHttpClient();
            customTransport = new WebsocketCustomTransport(client);
            var autoTransport = new AutoTransport(client, new IClientTransport[] { customTransport });
            hubConnection.TransportConnectTimeout = hubConnection.DeadlockErrorTimeout = TimeSpan.FromSeconds(10.0);

            // setup connect event
            customTransport.WebSocket.Connected += (ws) =>
            {
                lock (sockets)
                {
                    foreach (IWebSocket socket in sockets)
                    {
                        (socket as SignalrSocketConnection).InvokeConnected();
                    }
                }
            };

            // setup disconnect event
            customTransport.WebSocket.Disconnected += (ws) =>
            {
                lock (sockets)
                {
                    foreach (IWebSocket socket in sockets)
                    {
                        (socket as SignalrSocketConnection).InvokeDisconnected();
                    }
                }

                // start a task to tear down the hub connection
                Task.Run(() =>
                {
                    try
                    {
                        // tear down the hub connection, we must re-create it whenever a web socket disconnects
                        hubConnection?.Dispose();
                    }
                    catch
                    {
                    }
                });
            };
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
                foreach (string[] p in listener.Param)
                {
                    await hubProxy.Invoke<bool>(listener.FunctionFullName, p);
                }
            }
        }

        /// <summary>
        /// Stop the hub connection
        /// </summary>
        public void Stop()
        {
            hubConnection.Stop(TimeSpan.FromSeconds(1.0));
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
            disposed = true;
            if (hubConnection == null)
            {
                return;
            }
            listeners.Clear();

            // null out hub so we don't try to reconnect
            var tmp = hubConnection;
            hubConnection = null;
            try
            {
                tmp.Transport.Dispose();
                tmp.Dispose();
            }
            catch
            {
                // eat exceptions here, we don't care if it fails
            }
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
