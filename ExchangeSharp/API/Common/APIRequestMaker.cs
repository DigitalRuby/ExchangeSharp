﻿/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Handles all the logic for making API calls.
    /// </summary>
    /// <seealso cref="ExchangeSharp.IAPIRequestMaker" />
    public sealed class APIRequestMaker : IAPIRequestMaker
    {
        private readonly IAPIRequestHandler api;

        private class InternalHttpWebRequest : IHttpWebRequest
        {
            internal readonly HttpWebRequest request;

            public InternalHttpWebRequest(Uri fullUri)
            {
                request = HttpWebRequest.Create(fullUri) as HttpWebRequest;
                request.KeepAlive = false;
            }

            public void AddHeader(string header, string value)
            {
                switch (header.ToStringLowerInvariant())
                {
                    case "content-type":
                        request.ContentType = value;
                        break;

                    case "content-length":
                        request.ContentLength = value.ConvertInvariant<long>();
                        break;

                    case "user-agent":
                        request.UserAgent = value;
                        break;

                    case "accept":
                        request.Accept = value;
                        break;

                    case "connection":
                        request.Connection = value;
                        break;

                    default:
                        request.Headers[header] = value;
                        break;
                }
            }

            public Uri RequestUri
            {
                get { return request.RequestUri; }
            }

            public string Method
            {
                get { return request.Method; }
                set { request.Method = value; }
            }

            public int Timeout
            {
                get { return request.Timeout; }
                set { request.Timeout = value; }
            }

            public int ReadWriteTimeout
            {
                get { return request.ReadWriteTimeout; }
                set { request.ReadWriteTimeout = value; }
            }

            public async Task WriteAllAsync(byte[] data, int index, int length)
            {
                using (Stream stream = await request.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(data, 0, data.Length);
                }
            }
        }

        private class InternalHttpWebResponse : IHttpWebResponse
        {
            private readonly HttpWebResponse response;

            public InternalHttpWebResponse(HttpWebResponse response)
            {
                this.response = response;
            }

            public IReadOnlyList<string> GetHeader(string name)
            {
                return response.Headers.GetValues(name) ?? CryptoUtility.EmptyStringArray;
            }

            public Dictionary<string, IReadOnlyList<string>> Headers
            {
                get
                {
                    Dictionary<string, IReadOnlyList<string>> headers = new Dictionary<string, IReadOnlyList<string>>();
                    foreach (var header in response.Headers.AllKeys)
                    {
                        headers[header] = new List<string>(response.Headers.GetValues(header));
                    }
                    return headers;
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="api">API</param>
        public APIRequestMaker(IAPIRequestHandler api)
        {
            this.api = api;
        }

        /// <summary>
        /// Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// The encoding of payload is API dependant but is typically json.</param>
        /// <param name="method">Request method or null for default. Example: 'GET' or 'POST'.</param>
        /// <returns>Raw response</returns>
        public async Task<string> MakeRequestAsync(string url, string baseUrl = null, Dictionary<string, object> payload = null, string method = null)
        {
            await new SynchronizationContextRemover();

            await api.RateLimit.WaitToProceedAsync();
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }
            else if (url[0] != '/')
            {
                url = "/" + url;
            }

            string fullUrl = (baseUrl ?? api.BaseUrl) + url;
            method = method ?? api.RequestMethod;
            Uri uri = api.ProcessRequestUrl(new UriBuilder(fullUrl), payload, method);
            InternalHttpWebRequest request = new InternalHttpWebRequest(uri)
            {
                Method = method
            };
            request.AddHeader("accept-language", "en-US,en;q=0.5");
            request.AddHeader("content-type", api.RequestContentType);
            request.AddHeader("user-agent", BaseAPI.RequestUserAgent);
            request.Timeout = request.ReadWriteTimeout = (int)api.RequestTimeout.TotalMilliseconds;
            await api.ProcessRequestAsync(request, payload);
            HttpWebResponse response = null;
            string responseString = null;

            try
            {
                try
                {
                    RequestStateChanged?.Invoke(this, RequestMakerState.Begin, uri.AbsoluteUri);// when start make a request we send the uri, this helps developers to track the http requests.
                    response = await request.request.GetResponseAsync() as HttpWebResponse;
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
                using (Stream responseStream = response.GetResponseStream())
                {
                    responseString = new StreamReader(responseStream).ReadToEnd();
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        // 404 maybe return empty responseString
                        if (string.IsNullOrWhiteSpace(responseString))
                        {
                            throw new APIException(string.Format("{0} - {1}",
                                response.StatusCode.ConvertInvariant<int>(), response.StatusCode));
                        }
                        throw new APIException(responseString);
                    }
                    api.ProcessResponse(new InternalHttpWebResponse(response));
                    RequestStateChanged?.Invoke(this, RequestMakerState.Finished, responseString);
                }
            }
            catch (Exception ex)
            {
                RequestStateChanged?.Invoke(this, RequestMakerState.Error, ex);
                throw;
            }
            finally
            {
                response?.Dispose();
            }
            return responseString;
        }

        /// <summary>
        /// An action to execute when a request has been made (this request and state and object (response or exception))
        /// </summary>
        public Action<IAPIRequestMaker, RequestMakerState, object> RequestStateChanged { get; set; }
    }
}
