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
using System.Net;
using System.Threading.Tasks;

namespace ExchangeSharp
{
    /// <summary>
    /// Request maker states
    /// </summary>
    public enum RequestMakerState
    {
        /// <summary>
        /// About to begin request
        /// </summary>
        Begin,

        /// <summary>
        /// Request finished successfully
        /// </summary>
        Finished,

        /// <summary>
        /// Request error
        /// </summary>
        Error
    }

    /// <summary>
    /// Interface for making API requests
    /// </summary>
    public interface IAPIRequestMaker
    {
        /// <summary>
        /// Make a request to a path on the API
        /// </summary>
        /// <param name="url">Path and query</param>
        /// <param name="baseUrl">Override the base url, null for the default BaseUrl</param>
        /// <param name="payload">Payload, can be null. For private API end points, the payload must contain a 'nonce' key set to GenerateNonce value.</param>
        /// The encoding of payload is API dependant but is typically json.</param>
        /// <param name="method">Request method or null for default</param>
        /// <returns>Raw response</returns>
        /// <exception cref="System.Exception">Request fails</exception>
        Task<string> MakeRequestAsync(string url, string? baseUrl = null, Dictionary<string, object>? payload = null, string? method = null);

        /// <summary>
        /// An action to execute when a request has been made (this request and state and object (response or exception))
        /// </summary>
        Action<IAPIRequestMaker, RequestMakerState, object>? RequestStateChanged { get; set; }
    }

    /// <summary>
    /// Http web request
    /// </summary>
    public interface IHttpWebRequest
    {
        /// <summary>
        /// Request uri
        /// </summary>
        Uri RequestUri { get; }

        /// <summary>
        /// Request method (GET, POST, PUT, DELETE, etc.)
        /// </summary>
        string Method { get; set; }

        /// <summary>
        /// Response timeout
        /// </summary>
        int Timeout { get; set; }

        /// <summary>
        /// Read/write timeout
        /// </summary>
        int ReadWriteTimeout { get; set; }

        /// <summary>
        /// Add a header
        /// </summary>
        /// <param name="header">Header</param>
        /// <param name="value">Value</param>
        void AddHeader(string header, string value);

        /// <summary>
        /// Write data to the request and then flush, get ready for reading response
        /// </summary>
        /// <param name="data">Data</param>
        /// <param name="index">Offset</param>
        /// <param name="length">Length</param>
        /// <returns></returns>
        Task WriteAllAsync(byte[] data, int index, int length);
    }

    /// <summary>
    /// Http web response
    /// </summary>
    public interface IHttpWebResponse
    {
        /// <summary>
        /// Get header by name
        /// </summary>
        /// <param name="name">Header name</param>
        /// <returns>Header values, count of 0 if header not exist, will never return null</returns>
        IReadOnlyList<string> GetHeader(string name);

        /// <summary>
        /// Headers
        /// </summary>
        Dictionary<string, IReadOnlyList<string>> Headers { get; }
    }

    /// <summary>
    /// Interface for setting up and handling API request and response
    /// </summary>
    public interface IAPIRequestHandler
    {
        /// <summary>
        /// Additional handling for request
        /// </summary>
        /// <param name="request">Request</param>
        /// <param name="payload">Payload</param>
        Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object>? payload);

        /// <summary>
        /// Additional handling for response
        /// </summary>
        /// <param name="response">Response</param>
        void ProcessResponse(IHttpWebResponse response);

        /// <summary>
        /// Process a request url
        /// </summary>
        /// <param name="url">Url</param>
        /// <param name="payload">Payload</param>
        /// <param name="method">Method</param>
        /// <returns>Updated url</returns>
        Uri ProcessRequestUrl(UriBuilder url, Dictionary<string, object>? payload, string method);

        /// <summary>
        /// Base url for the request
        /// </summary>
        string BaseUrl { get; }

        /// <summary>
        /// Request method, i.e. GET
        /// </summary>
        string RequestMethod { get; }

        /// <summary>
        /// Request content type, i.e. application/json
        /// </summary>
        string RequestContentType { get; }

        /// <summary>
        /// Request cache policy
        /// </summary>
        System.Net.Cache.RequestCachePolicy RequestCachePolicy { get; }
        
        /// <summary>
        /// Request timeout, this will get assigned to the request before sending it off
        /// </summary>
        TimeSpan RequestTimeout { get; }

        /// <summary>
        /// Rate limiter
        /// </summary>
        RateGate RateLimit { get; }
    }
}
