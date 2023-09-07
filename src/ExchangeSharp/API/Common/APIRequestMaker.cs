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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
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

		/// <summary>
		/// Proxy for http requests, reads from HTTP_PROXY environment var by default
		/// You can also set via code if you like
		/// </summary>
		private static readonly HttpClientHandler ClientHandler = new HttpClientHandler();
		public static IWebProxy? Proxy
		{
			get => ClientHandler.Proxy;
			set { ClientHandler.Proxy = value; }
		}
		public static readonly HttpClient Client = new HttpClient(ClientHandler);

		/// <summary>
		/// Static constructor
		/// </summary>
		static APIRequestMaker()
		{
			var httpProxy = Environment.GetEnvironmentVariable("http_proxy");
			httpProxy ??= Environment.GetEnvironmentVariable("HTTP_PROXY");

			if (!string.IsNullOrWhiteSpace(httpProxy))
			{
				var uri = new Uri(httpProxy);
				Proxy = new WebProxy(uri);
			}

			Client.DefaultRequestHeaders.ConnectionClose = true; // disable keep-alive
			Client.Timeout = Timeout.InfiniteTimeSpan; // we handle timeout by ourselves
		}

		internal class InternalHttpWebRequest : IHttpWebRequest
		{
			internal readonly HttpRequestMessage Request;
			internal HttpResponseMessage? Response;
			private string? contentType;

			public InternalHttpWebRequest(string method, Uri fullUri)
			{
				Request = new HttpRequestMessage(new HttpMethod(method), fullUri);
			}

			public void AddHeader(string header, string value)
			{
				switch (header.ToLowerInvariant())
				{
					case "content-type":
						contentType = value;
						break;
					default:
						Request.Headers.TryAddWithoutValidation(header, value);
						break;
				}
			}

			public Uri RequestUri
			{
				get { return Request.RequestUri; }
			}

			public string Method
			{
				get { return Request.Method.Method; }
				set { Request.Method = new HttpMethod(value); }
			}

			public int Timeout { get; set; }

			public int ReadWriteTimeout
			{
				get => Timeout;
				set => Timeout = value;
			}

			public Task WriteAllAsync(byte[] data, int index, int length)
			{
				Request.Content = new ByteArrayContent(data, index, length);
				Request.Content.Headers.Add("content-type", contentType);
				return Task.CompletedTask;
			}
		}

		internal class InternalHttpWebResponse : IHttpWebResponse
		{
			private readonly HttpResponseMessage response;

			public InternalHttpWebResponse(HttpResponseMessage response)
			{
				this.response = response;
			}

			public IReadOnlyList<string> GetHeader(string name)
			{
				if (response.Headers.TryGetValues(name: name, out var header))
				{
					return header.ToArray();
				}
				else
				{
					return CryptoUtility.EmptyStringArray;
				}
			}

			public Dictionary<string, IReadOnlyList<string>> Headers
			{
				get
				{
					return response.Headers.ToDictionary(
							x => x.Key,
							x => (IReadOnlyList<string>)x.Value.ToArray()
					);
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
		public async Task<IAPIRequestMaker.RequestResult<string>> MakeRequestAsync(
				string url,
				string? baseUrl = null,
				Dictionary<string, object>? payload = null,
				string? method = null
		)
		{
			await new SynchronizationContextRemover();
			await api.RateLimit.WaitToProceedAsync();

			if (url[0] != '/')
			{
				url = "/" + url;
			}

			// prepare the request
			string fullUrl = (baseUrl ?? api.BaseUrl) + url;
			method ??= api.RequestMethod;
			Uri uri = api.ProcessRequestUrl(new UriBuilder(fullUrl), payload, method);
			var request = new InternalHttpWebRequest(method, uri);
			request.AddHeader("accept-language", "en-US,en;q=0.5");
			request.AddHeader("content-type", api.RequestContentType);
			request.AddHeader("user-agent", BaseAPI.RequestUserAgent);
			request.Timeout = (int)api.RequestTimeout.TotalMilliseconds;
			await api.ProcessRequestAsync(request, payload);

			// send the request
			var response = request.Response;
			string responseString;
			using var cancel = new CancellationTokenSource(request.Timeout);
			try
			{
				RequestStateChanged?.Invoke(this, RequestMakerState.Begin, uri.AbsoluteUri); // when start make a request we send the uri, this helps developers to track the http requests.
				response = await Client.SendAsync(request.Request, cancel.Token);
				if (response == null)
				{
					throw new APIException("Unknown response from server");
				}
				responseString = await response.Content.ReadAsStringAsync();

				if (
						response.StatusCode != HttpStatusCode.OK
						&& response.StatusCode != HttpStatusCode.Created
				)
				{
					// 404 maybe return empty responseString
					if (string.IsNullOrWhiteSpace(responseString))
					{
						throw new APIException(
								string.Format(
										"{0} - {1}",
										response.StatusCode.ConvertInvariant<int>(),
										response.StatusCode
								)
						);
					}

					throw new APIException(responseString);
				}

				api.ProcessResponse(new InternalHttpWebResponse(response));
				RequestStateChanged?.Invoke(this, RequestMakerState.Finished, responseString);
			}
			catch (OperationCanceledException ex) when (cancel.IsCancellationRequested)
			{
				RequestStateChanged?.Invoke(this, RequestMakerState.Error, ex);
				throw new TimeoutException("APIRequest timeout", ex);
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
			return new IAPIRequestMaker.RequestResult<string>()
			{
				Response = responseString,
				HTTPHeaderDate = response.Headers.Date
			};
		}

		/// <summary>
		/// An action to execute when a request has been made (this request and state and object (response or exception))
		/// </summary>
		public Action<
				IAPIRequestMaker,
				RequestMakerState,
				object
		>? RequestStateChanged
		{ get; set; }
	}
}
