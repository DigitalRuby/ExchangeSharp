﻿/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    /// <summary>
    /// Exception class for API calls
    /// </summary>
    public class APIException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public APIException(string message) : base(message) { }
    }

    /// <summary>
    /// API base class functionality
    /// </summary>
    public abstract class BaseAPI
    {
        /// <summary>
        /// Base URL for the API
        /// </summary>
        public abstract string BaseUrl { get; set; }

        /// <summary>
        /// Gets the name of the API
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Public API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// </summary>
        public System.Security.SecureString PublicApiKey { get; set; }

        /// <summary>
        /// Private API key - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// </summary>
        public System.Security.SecureString PrivateApiKey { get; set; }

        /// <summary>
        /// Pass phrase API key  - only needs to be set if you are using private authenticated end points. Please use CryptoUtility.SaveUnprotectedStringsToFile to store your API keys, never store them in plain text!
        /// Most services do not require this, but GDAX is an example of one that does
        /// </summary>
        public System.Security.SecureString Passphrase { get; set; }

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
        /// User agent for requests
        /// </summary>
        public string RequestUserAgent { get; set; } = "ExchangeSharp (https://github.com/jjxtra/ExchangeSharp)";

        /// <summary>
        /// Timeout for requests
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30.0);

        /// <summary>
        /// Cache policy - defaults to no cache, don't change unless you have specific needs
        /// </summary>
        public System.Net.Cache.RequestCachePolicy CachePolicy { get; set; } = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

        private readonly Dictionary<string, KeyValuePair<DateTime, object>> cache = new Dictionary<string, KeyValuePair<DateTime, object>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Load API keys from an encrypted file - keys will stay encrypted in memory
        /// </summary>
        /// <param name="encryptedFile">Encrypted file to load keys from</param>
        public virtual void LoadAPIKeys(string encryptedFile)
        {
            SecureString[] strings = CryptoUtility.LoadProtectedStringsFromFile(encryptedFile);
            if (strings.Length < 2)
            {
                throw new InvalidOperationException("Encrypted keys file should have a public and private key, and an optional pass phrase");
            }
            PublicApiKey = strings[0];
            PrivateApiKey = strings[1];
            if (strings.Length > 2)
            {
                Passphrase = strings[3];
            }
        }

        /// <summary>
        /// Load API keys from unsecure strings
        /// </summary>
        /// <param name="publicApiKey">Public Api Key</param>
        /// <param name="privateApiKey">Private Api Key</param>
        public virtual void LoadAPIKeysUnsecure(string publicApiKey, string privateApiKey)
        {
            PublicApiKey = publicApiKey.ToSecureString();
            PrivateApiKey = privateApiKey.ToSecureString();
        }

        /// <summary>
        /// Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a string value, set to unix timestamp in milliseconds, or seconds with decimal depending on the API.</param>
        /// The encoding of payload is API dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        public string MakeRequest(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null)
        {
            RateLimit.WaitToProceed();
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            else if (url[0] != '/')
            {
                url = "/" + url;
            }

            string fullUrl = (baseUrl ?? BaseUrl) + url;
            Uri uri = ProcessRequestUrl(new UriBuilder(fullUrl), payload);
            HttpWebRequest request = HttpWebRequest.CreateHttp(uri);
            request.Method = method ?? RequestMethod;
            request.ContentType = RequestContentType;
            request.UserAgent = RequestUserAgent;
            request.CachePolicy = CachePolicy;
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            ProcessRequest(request, payload);
            HttpWebResponse response;
            try
            {
                response = request.GetResponse() as HttpWebResponse;
                if (response == null)
                {
                    throw new APIException("Unknown response from server");
                }
            }
            catch (WebException we)
            {
                response = we.Response as HttpWebResponse;
                if (response == null)
                {
                    throw new APIException(we.Message ?? "Unknown response from server");
                }
            }
            string responseString = null;
            using (Stream responseStream = response.GetResponseStream())
            {
                responseString = new StreamReader(responseStream).ReadToEnd();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new APIException(responseString);
                }
                ProcessResponse(response);
            }
            response.Dispose();
            return responseString;
        }

        /// <summary>
        /// ASYNC - Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a string value, set to unix timestamp in milliseconds, or seconds with decimal depending on the API.</param>
        /// The encoding of payload is API dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        public Task<string> MakeRequestAsync(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null) => Task.Factory.StartNew(() => MakeRequest(url, baseUrl, payload, method));

        /// <summary>
        /// Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a string value, set to unix timestamp in milliseconds, or seconds with decimal depending on the API.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        public T MakeJsonRequest<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null)
        {
            string response = MakeRequest(url, baseUrl, payload, requestMethod);
            return JsonConvert.DeserializeObject<T>(response);
        }

        /// <summary>
        /// ASYNC - Make a JSON request to an API end point
        /// </summary>
        /// <typeparam name="T">Type of object to parse JSON as</typeparam>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key with a string value, set to unix timestamp in milliseconds, or seconds with decimal depending on the API.</param>
        /// <param name="requestMethod">Request method or null for default</param>
        /// <returns>Result decoded from JSON response</returns>
        public Task<T> MakeJsonRequestAsync<T>(string url, string baseUrl = null, Dictionary<string, object> payload = null, string requestMethod = null) => Task.Factory.StartNew(() => MakeJsonRequest<T>(url, baseUrl, payload, requestMethod));

        /// <summary>
        /// Whether the API can make authenticated (private) API requests
        /// </summary>
        /// <param name="payload">Payload to potentially send</param>
        /// <returns>True if an authenticated request can be made with the payload, false otherwise</returns>
        protected virtual bool CanMakeAuthenticatedRequest(IReadOnlyDictionary<string, object> payload)
        {
            return (PrivateApiKey != null && PublicApiKey != null && payload != null && payload.ContainsKey("nonce"));
        }

        /// <summary>
        /// Process a request url
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="payload">Payload</param>
        /// <returns>Updated url</returns>
        protected virtual Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object> payload)
        {
            return url.Uri;
        }

        /// <summary>
        /// Additional handling for request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        protected virtual void ProcessRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {

        }

        /// <summary>
        /// Additional handling for response
        /// </summary>
        /// <param name="response">Response</param>
        protected virtual void ProcessResponse(HttpWebResponse response)
        {

        }

        /// <summary>
        /// Get a form for a request (form-encoded, like a query string)
        /// </summary>
        /// <param name="payload">Payload</param>
        /// <param name="includeNonce">Whether to add the nonce</param>
        /// <returns>Form string</returns>
        protected string GetFormForPayload(Dictionary<string, object> payload, bool includeNonce = true)
        {
            if (payload != null && payload.Count != 0)
            {
                StringBuilder form = new StringBuilder();
                foreach (KeyValuePair<string, object> keyValue in payload)
                {
                    if (includeNonce || keyValue.Key != "nonce")
                    {
                        form.AppendFormat("{0}={1}&", Uri.EscapeDataString(keyValue.Key), Uri.EscapeDataString(keyValue.Value.ToString()));
                    }
                }
                if (form.Length != 0)
                {
                    form.Length--; // trim ampersand
                }
                return form.ToString();
            }
            return string.Empty;
        }

        /// <summary>
        /// Convert a payload into json
        /// </summary>
        /// <param name="payload">Payload</param>
        /// <returns>Json string</returns>
        protected string GetJsonForPayload(Dictionary<string, object> payload)
        {
            if (payload != null && payload.Count != 0)
            {
                return JsonConvert.SerializeObject(payload);
            }
            return string.Empty;
        }

        /// <summary>
        /// Write a form to a request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="form">Form to write</param>
        protected void WriteFormToRequest(HttpWebRequest request, string form)
        {
            if (!string.IsNullOrEmpty(form))
            {
                using (StreamWriter writer = new StreamWriter(request.GetRequestStream(), Encoding.ASCII))
                {
                    writer.Write(form);
                }
            }
        }

        /// <summary>
        /// Write a payload to a request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        /// <returns>The form string that was written</returns>
        protected string WritePayloadToRequest(HttpWebRequest request, Dictionary<string, object> payload)
        {
            string form = GetFormForPayload(payload);
            WriteFormToRequest(request, form);
            return form;
        }

        /// <summary>
        /// Read a value from the cache
        /// </summary>
        /// <typeparam name="T">Type to read</typeparam>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <returns>True if read, false if not. If false, value is default(T).</returns>
        protected bool ReadCache<T>(string key, out T value)
        {
            lock (cache)
            {
                if (cache.TryGetValue(key, out KeyValuePair<DateTime, object> cacheValue))
                {
                    // if not expired, return
                    if (cacheValue.Key > DateTime.UtcNow)
                    {
                        value = (T)cacheValue.Value;
                        return true;
                    }
                    cache.Remove(key);
                }
            }
            value = default(T);
            return false;
        }

        /// <summary>
        /// Write a value to the cache
        /// </summary>
        /// <typeparam name="T">Type of value</typeparam>
        /// <param name="key">Key</param>
        /// <param name="expiration">Expiration from now</param>
        /// <param name="value">Value</param>
        protected void WriteCache<T>(string key, TimeSpan expiration, T value)
        {
            lock (cache)
            {
                cache[key] = new KeyValuePair<DateTime, object>(DateTime.UtcNow + expiration, value);
            }
        }
    }
}
