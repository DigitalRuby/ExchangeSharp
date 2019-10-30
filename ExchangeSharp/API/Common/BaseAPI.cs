/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// Type of nonce styles
    /// </summary>
    public enum NonceStyle
    {
        /// <summary>
        /// No nonce style
        /// </summary>
        None = 0,

        /// <summary>
        /// Ticks (int64)
        /// </summary>
        Ticks,

        /// <summary>
        /// Ticks (string)
        /// </summary>
        TicksString,

        /// <summary>
        /// Start with ticks, then increment by one
        /// </summary>
        TicksThenIncrement,

        /// <summary>
        /// Milliseconds (int64)
        /// </summary>
        UnixMilliseconds,

        /// <summary>
        /// Milliseconds (string)
        /// </summary>
        UnixMillisecondsString,

        /// <summary>
        /// Start with Unix milliseconds then increment by one
        /// </summary>
        UnixMillisecondsThenIncrement,

        /// <summary>
        /// Seconds (double)
        /// </summary>
        UnixSeconds,

        /// <summary>
        /// Seconds (string)
        /// </summary>
        UnixSecondsString,

        /// <summary>
        /// Persist nonce to counter and file for the API key, once it hits int.MaxValue, it is useless
        /// </summary>
        Int32File,

        /// <summary>
        /// Persist nonce to counter and file for the API key, once it hits long.MaxValue, it is useless
        /// </summary>
        Int64File,

        /// <summary>
        /// No nonce, use expires instead which passes an expires param to the api using the nonce value - duplicate nonce are allowed
        /// Specify a negative NonceOffset for when you want the call to expire
        /// </summary>
        ExpiresUnixSeconds,

        /// <summary>
        /// No nonce, use expires instead which passes an expires param to the api using the nonce value - duplicate nonce are allowed
        /// Specify a negative NonceOffset for when you want the call to expire
        /// </summary>
        ExpiresUnixMilliseconds,

        /// <summary>
        /// An iso-8601 date
        /// </summary>
        Iso8601
    }

    /// <summary>
    /// Anything wit ha name
    /// </summary>
    public interface INamed
    {
        /// <summary>
        /// The name of the service, exchange, etc.
        /// </summary>
        string Name { get; }
    }

    /// <summary>
    /// API base class functionality
    /// </summary>
    public abstract class BaseAPI : IAPIRequestHandler, INamed
    {
        /// <summary>
        /// User agent for requests
        /// </summary>
        public const string RequestUserAgent = "ExchangeSharp (https://github.com/jjxtra/ExchangeSharp)";

        private IAPIRequestMaker requestMaker;
        /// <summary>
        /// API request maker
        /// </summary>
        public IAPIRequestMaker RequestMaker
        {
            get { return requestMaker; }
            set { requestMaker = value ?? new APIRequestMaker(this); }

        }
        /// <summary>
        /// Base URL for the API
        /// </summary>
        public abstract string BaseUrl { get; set; }

        /// <summary>
        /// Base URL for the API for web sockets
        /// </summary>
        public virtual string BaseUrlWebSocket { get; set; } = string.Empty;

        /// <summary>
        /// Gets the name of the API
        /// </summary>
        public virtual string Name { get; private set; } = string.Empty;

        /// <summary>
        /// Public API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// </summary>
        public System.Security.SecureString? PublicApiKey { get; set; }

        /// <summary>
        /// Private API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// </summary>
        public System.Security.SecureString? PrivateApiKey { get; set; }

        /// <summary>
        /// Pass phrase API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// Most services do not require this, but Coinbase is an example of one that does
        /// </summary>
        public System.Security.SecureString? Passphrase { get; set; }

        /// <summary>
        /// Rate limiter - set this to a new limit if you are seeing your ip get blocked by the API
        /// </summary>
        public RateGate RateLimit { get; set; } = new RateGate(5, TimeSpan.FromSeconds(15.0d));

        /// <summary>
        /// Default request method
        /// </summary>
        public string RequestMethod { get; set; } = "GET";

        /// <summary>
        /// Content type for requests
        /// </summary>
        public string RequestContentType { get; set; } = "text/plain";

        /// <summary>
        /// Timeout for requests
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30.0);

        /// <summary>
        /// Request window - most services do not use this, but Binance API is an example of one that does
        /// </summary>
        public TimeSpan RequestWindow { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Type of nonce
        /// </summary>
        public NonceStyle NonceStyle { get; protected set; } = NonceStyle.Ticks;

        /// <summary>
        /// Offset for nonce calculation, some exchanges like Binance have a problem with requests being in the future, so you can offset the current DateTime with this
        /// </summary>
        public TimeSpan NonceOffset { get; set; }

        /// <summary>
        /// The nonce end point for pulling down a server timestamp - override OnGetNonceOffset if you need custom handling
        /// </summary>
        public string? NonceEndPoint { get; protected set; }

        /// <summary>
        /// The field in the json returned by the nonce end point to parse out - override OnGetNonceOffset if you need custom handling
        /// </summary>
        public string? NonceEndPointField { get; protected set; }

        /// <summary>
        /// The type of value in the nonce end point field - override OnGetNonceOffset if you need custom handling.
        /// Supported values are Iso8601 and UnixMilliseconds.
        /// </summary>
        public NonceStyle NonceEndPointStyle { get; protected set; }

        /// <summary>
        /// Cache policy - defaults to no cache, don't change unless you have specific needs
        /// </summary>
        public System.Net.Cache.RequestCachePolicy RequestCachePolicy { get; set; } = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

        /// <summary>
        /// Method cache policy (method name, time to cache)
        /// Can be cleared for no caching, or you can put in custom cache times using nameof(method) and timespan.
        /// </summary>
        public Dictionary<string, TimeSpan> MethodCachePolicy { get; } = new Dictionary<string, TimeSpan>();

        private ICache cache = new MemoryCache();
        /// <summary>
        /// Get or set the current cache. Defaults to MemoryCache.
        /// </summary>
        public ICache Cache
        {
            get { return cache; }
            set
            {
                value.ThrowIfNull(nameof(Cache));
                cache = value;
            }
        }

        private decimal lastNonce;

        private readonly string[] resultKeys = new string[] { "result", "data", "return", "Result", "Data", "Return" };

        /// <summary>
        /// Static constructor
        /// </summary>
        static BaseAPI()
        {

#pragma warning disable CS0618

            try
            {

#if HAS_WINDOWS_FORMS // NET47

                ServicePointManager.SecurityProtocol = SecurityProtocolType.SystemDefault | SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

#else

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

#endif

            }
            catch
            {

            }

#pragma warning restore CS0618

        }

        /// <summary>
        /// Constructor
        /// </summary>
        public BaseAPI()
        {
            requestMaker = new APIRequestMaker(this);

            string className = GetType().Name;
            object[] nameAttributes = GetType().GetCustomAttributes(typeof(ApiNameAttribute), true);
            if (nameAttributes == null || nameAttributes.Length == 0)
            {
                Name = Regex.Replace(className, "^Exchange|API$", string.Empty, RegexOptions.CultureInvariant);
            }
            else
            {
                Name = (nameAttributes[0] as ApiNameAttribute)?.Name ?? string.Empty;
            }
        }

        /// <summary>
        /// Generate a nonce
        /// </summary>
        /// <returns>Nonce</returns>
        public async Task<object> GenerateNonceAsync()
        {
            await new SynchronizationContextRemover();

            if (NonceOffset.Ticks == 0)
            {
                await OnGetNonceOffset();
            }

            object nonce;

            while (true)
            {
                lock (this)
                {
                    // some API (Binance) have a problem with requests being after server time, subtract of offset can help
                    DateTime now = CryptoUtility.UtcNow - NonceOffset;
                    switch (NonceStyle)
                    {
                        case NonceStyle.Ticks:
                            nonce = now.Ticks;
                            break;

                        case NonceStyle.TicksString:
                        case NonceStyle.Iso8601:
                            nonce = now.Ticks.ToStringInvariant();
                            break;

                        case NonceStyle.TicksThenIncrement:
                            if (lastNonce == 0m)
                            {
                                nonce = now.Ticks;
                            }
                            else
                            {
                                nonce = (long)(lastNonce + 1m);
                            }
                            break;

                        case NonceStyle.UnixMilliseconds:
                            nonce = (long)now.UnixTimestampFromDateTimeMilliseconds();
                            break;

                        case NonceStyle.UnixMillisecondsString:
                            nonce = ((long)now.UnixTimestampFromDateTimeMilliseconds()).ToStringInvariant();
                            break;

                        case NonceStyle.UnixMillisecondsThenIncrement:
                            if (lastNonce == 0m)
                            {
                                nonce = (long)now.UnixTimestampFromDateTimeMilliseconds();
                            }
                            else
                            {
                                nonce = (long)(lastNonce + 1m);
                            }
                            break;

                        case NonceStyle.UnixSeconds:
                            nonce = now.UnixTimestampFromDateTimeSeconds();
                            break;

                        case NonceStyle.UnixSecondsString:
                            nonce = now.UnixTimestampFromDateTimeSeconds().ToStringInvariant();
                            break;

                        case NonceStyle.Int32File:
                        case NonceStyle.Int64File:
                            {
                                // why an API would use a persistent incrementing counter for nonce is beyond me, ticks is so much better with a sliding window...
                                // making it required to increment by 1 is also a pain - especially when restarting a process or rebooting.
                                string tempFile = Path.Combine(Path.GetTempPath(), (PublicApiKey?.ToUnsecureString() ?? "unknown_pub_key") + ".nonce");
                                if (!File.Exists(tempFile))
                                {
                                    File.WriteAllText(tempFile, "0");
                                }
                                unchecked
                                {
                                    long longNonce = File.ReadAllText(tempFile).ConvertInvariant<long>() + 1;
                                    long maxValue = (NonceStyle == NonceStyle.Int32File ? int.MaxValue : long.MaxValue);
                                    if (longNonce < 1 || longNonce > maxValue)
                                    {
                                        throw new APIException($"Nonce {longNonce.ToStringInvariant()} is out of bounds, valid ranges are 1 to {maxValue.ToStringInvariant()}, " +
                                            $"please regenerate new API keys. Please contact {Name} API support and ask them to change to a sensible nonce algorithm.");
                                    }
                                    File.WriteAllText(tempFile, longNonce.ToStringInvariant());
                                    nonce = longNonce;
                                }
                                break;
                            }

                        case NonceStyle.ExpiresUnixMilliseconds:
                            nonce = (long)now.UnixTimestampFromDateTimeMilliseconds();
                            break;

                        case NonceStyle.ExpiresUnixSeconds:
                            nonce = (long)now.UnixTimestampFromDateTimeSeconds();
                            break;

                        default:
                            throw new InvalidOperationException("Invalid nonce style: " + NonceStyle);
                    }

                    // check for duplicate nonce
                    decimal convertedNonce = nonce.ConvertInvariant<decimal>();
                    if (lastNonce != convertedNonce || NonceStyle == NonceStyle.ExpiresUnixSeconds || NonceStyle == NonceStyle.ExpiresUnixMilliseconds)
                    {
                        lastNonce = convertedNonce;
                        break;
                    }
                }

                // wait 1 millisecond for a new nonce
                await Task.Delay(1);
            }

            return nonce;
        }

        /// <summary>
        /// Load API keys from an encrypted file - keys will stay encrypted in memory
        /// </summary>
        /// <param name="encryptedFile">Encrypted file to load keys from</param>
        public void LoadAPIKeys(string encryptedFile)
        {
	        if (string.IsNullOrWhiteSpace(encryptedFile))
		        throw new ArgumentNullException(nameof(encryptedFile));
	        if (!File.Exists(encryptedFile))
		        throw new ArgumentException("Invalid key file.", nameof(encryptedFile));

	        var strings = CryptoUtility.LoadProtectedStringsFromFile(encryptedFile);

	        if (strings.Length < 2)
	        {
		        throw new InvalidOperationException(
			        "Encrypted keys file should have at least a public and private key, and an optional pass phrase"
		        );
	        }

	        PublicApiKey = strings[0];
	        PrivateApiKey = strings[1];
	        if (strings.Length > 2)
	        {
		        Passphrase = strings[2];
	        }
        }

        /// <summary>
        /// Load API keys from unsecure strings
        /// </summary>
        /// <param name="publicApiKey">Public Api Key</param>
        /// <param name="privateApiKey">Private Api Key</param>
        /// <param name="passPhrase">Pass phrase, null for none</param>
        public void LoadAPIKeysUnsecure(string publicApiKey, string privateApiKey, string? passPhrase = null)
        {
            PublicApiKey = publicApiKey.ToSecureString();
            PrivateApiKey = privateApiKey.ToSecureString();
            Passphrase = passPhrase?.ToSecureString();
        }

        /// <summary>
        /// Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// The encoding of payload is API dependant but is typically json.
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        public Task<string> MakeRequestAsync(string url, string? baseUrl = null, Dictionary<string, object>? payload = null, string? method = null) => requestMaker.MakeRequestAsync(url, baseUrl: baseUrl, payload: payload, method: method);

        /// <summary>
        /// Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        public async Task<T> MakeJsonRequestAsync<T>(string url, string? baseUrl = null, Dictionary<string, object>? payload = null, string? requestMethod = null)
        {
            await new SynchronizationContextRemover();

            string stringResult = await MakeRequestAsync(url, baseUrl: baseUrl, payload: payload, method: requestMethod);
            T jsonResult = JsonConvert.DeserializeObject<T>(stringResult);
            if (jsonResult is JToken token)
            {
                return (T)(object)CheckJsonResponse(token);
            }
            return jsonResult;
        }

        /// <summary>
        /// Connect a web socket to a path on the API and start listening, not all exchanges support this
        /// </summary>
        /// <param name="url">The sub url for the web socket, or null for none</param>
        /// <param name="messageCallback">Callback for messages</param>
        /// <param name="connectCallback">Connect callback</param>
        /// <returns>Web socket - dispose of the wrapper to shutdown the socket</returns>
        public Task<IWebSocket> ConnectWebSocketAsync
        (
            string url,
            Func<IWebSocket, byte[], Task> messageCallback,
            WebSocketConnectionDelegate? connectCallback = null,
            WebSocketConnectionDelegate? disconnectCallback = null
        )
        {
            if (messageCallback == null)
            {
                throw new ArgumentNullException(nameof(messageCallback));
            }

            string fullUrl = BaseUrlWebSocket + (url ?? string.Empty);
            ExchangeSharp.ClientWebSocket wrapper = new ExchangeSharp.ClientWebSocket
            {
                Uri = new Uri(fullUrl),
                OnBinaryMessage = messageCallback
            };
            if (connectCallback != null)
            {
                wrapper.Connected += connectCallback;
            }
            if (disconnectCallback != null)
            {
                wrapper.Disconnected += disconnectCallback;
            }
            wrapper.Start();
            return Task.FromResult<IWebSocket>(wrapper);
        }

        /// <summary>
        /// Whether the API can make authenticated (private) API requests
        /// </summary>
        /// <param name="payload">Payload to potentially send</param>
        /// <returns>True if an authenticated request can be made with the payload, false otherwise</returns>
        protected virtual bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object>? payload)
        {
            return (PrivateApiKey != null && PublicApiKey != null && payload != null && payload.ContainsKey("nonce"));
        }

        /// <summary>
        /// Additional handling for request. This simply returns a completed task and can be used for derived classes
        /// that do not have an await in their ProcessRequestAsync overload.
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        protected virtual Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object>? payload)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Additional handling for response
        /// </summary>
        /// <param name="response">Response</param>
        protected virtual void ProcessResponse(IHttpWebResponse response)
        {

        }

        /// <summary>
        /// Process a request url
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="payload">Payload</param>
        /// <param name="method">Method</param>
        /// <returns>Updated url</returns>
        protected virtual Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object>? payload, string? method)
        {
            return url.Uri;
        }

        /// <summary>
        /// Throw an exception if token represents an error condition.
        /// For most API this method does not need to be overriden if:
        /// - API passes an 'error', 'errorCode' or 'error_code' child element if the call fails
        /// - API passes a 'status' element of 'error' if the call fails
        /// - API passes a 'success' element of 'false' if the call fails
        /// This call also looks for 'result', 'data', 'return' child elements and returns those if
        /// found, otherwise the result parameter is returned.
        /// For all other cases, override CheckJsonResponse for the exchange or add more logic here.
        /// </summary>
        /// <param name="result">Result</param>
        protected virtual JToken CheckJsonResponse(JToken result)
        {
            if (result == null)
            {
                throw new APIException("No result from server");
            }
            else if (!(result is JArray) && result.Type == JTokenType.Object)
            {
                if
                (
                    (!string.IsNullOrWhiteSpace(result["error"].ToStringInvariant())) ||
                    (!string.IsNullOrWhiteSpace(result["errorCode"].ToStringInvariant())) ||
                    (!string.IsNullOrWhiteSpace(result["error_code"].ToStringInvariant())) ||
                    (result["status"].ToStringInvariant() == "error") ||
                    (result["Status"].ToStringInvariant() == "error") ||
                    (result["success"] != null && !result["success"].ConvertInvariant<bool>()) ||
                    (result["Success"] != null && !result["Success"].ConvertInvariant<bool>()) ||
                    (!string.IsNullOrWhiteSpace(result["ok"].ToStringInvariant()) && result["ok"].ToStringInvariant().ToLowerInvariant() != "ok")
                )
                {
                    throw new APIException(result.ToStringInvariant());
                }

                // find result object from result keywords
                foreach (string key in resultKeys)
                {
                    JToken possibleResult = result[key];
                    if (possibleResult != null && (possibleResult.Type == JTokenType.Object || possibleResult.Type == JTokenType.Array))
                    {
                        result = possibleResult;
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Get a dictionary with a nonce key and value of the required nonce type. Derived classes should call this base class method first.
        /// </summary>
        /// <param name="key">Key</param>
        /// <returns>Dictionary with nonce</returns>
        protected virtual async Task<Dictionary<string, object>> GetNoncePayloadAsync()
        {
            Dictionary<string, object> noncePayload = new Dictionary<string, object>
            {
                ["nonce"] = await GenerateNonceAsync()
            };
            if (RequestWindow.Ticks > 0)
            {
                noncePayload["recvWindow"] = (long)RequestWindow.TotalMilliseconds;
            }
            return noncePayload;
        }

        /// <summary>
        /// Derived classes can override to get a nonce offset from the API itself
        /// </summary>
        protected virtual async Task OnGetNonceOffset()
        {
            if (String.IsNullOrWhiteSpace(NonceEndPoint))
            {
                return;
            }

            try
            {
                JToken token = await MakeJsonRequestAsync<JToken>(NonceEndPoint!);
                JToken value = token[NonceEndPointField];
                DateTime serverDate = NonceEndPointStyle switch
                {
                    NonceStyle.Iso8601 => value.ToDateTimeInvariant(),
                    NonceStyle.UnixMilliseconds => value.ConvertInvariant<long>().UnixTimeStampToDateTimeMilliseconds(),
                    _ => throw new ArgumentException("Invalid nonce end point style '" + NonceEndPointStyle + "' for exchange '" + Name + "'"),
                };
                NonceOffset = (CryptoUtility.UtcNow - serverDate);
            }
            catch
            {
                // if this fails we don't want to crash, just run without a nonce
                Logger.Warn("Failed to get nonce offset for exchange {0}", Name);
            }
        }

        async Task IAPIRequestHandler.ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object>? payload)
        {
            await ProcessRequestAsync(request, payload);
        }

        void IAPIRequestHandler.ProcessResponse(IHttpWebResponse response)
        {
            ProcessResponse(response);
        }

        Uri IAPIRequestHandler.ProcessRequestUrl(UriBuilder url, Dictionary<string, object>? payload, string? method)
        {
            return ProcessRequestUrl(url, payload, method);
        }
    }

    /// <summary>
    /// Normally a BaseAPI class attempts to get the Name from the class name.
    /// If there is a problem, apply this attribute to a BaseAPI subclass to populate the Name property with the attribute name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ApiNameAttribute : Attribute
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name">Name</param>
        public ApiNameAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"'{nameof(name)}' must not be null or empty");
            }
            Name = name;
        }

        /// <summary>
        /// Name
        /// </summary>
        public string Name { get; private set; }
    }
}
