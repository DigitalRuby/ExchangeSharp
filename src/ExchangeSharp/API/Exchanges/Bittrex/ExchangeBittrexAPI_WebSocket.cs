/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

// if you can't use ASP.NET signalr nuget package, comment this out
#define HAS_SIGNALR

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Security.Cryptography;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace ExchangeSharp
{
	public partial class ExchangeBittrexAPI : ExchangeAPI
	{
#if HAS_SIGNALR
		const string URL = "https://socket-v3.bittrex.com/signalr";

		public string[] channels = new string[] {
				"heartbeat",
				"trade_BTC-USD",
				"order"};

		public async Task<SocketClient> ConnectAndAuthenticate()
		{
			var client = new SocketClient(URL);
			if (await client.Connect())
			{
				Console.WriteLine("Connected");
			}
			else
			{
				Console.WriteLine("Failed to connect");
				return client;
			}

			if (!string.IsNullOrWhiteSpace(CryptoUtility.ToUnsecureString(PrivateApiKey)))
			{
				await Authenticate(client, CryptoUtility.ToUnsecureString(PublicApiKey), CryptoUtility.ToUnsecureString(PrivateApiKey));
				client.SetAuthExpiringHandler(async () =>
				{
					Console.WriteLine("Authentication expiring...");
					await Authenticate(client, CryptoUtility.ToUnsecureString(PublicApiKey), CryptoUtility.ToUnsecureString(PrivateApiKey));
				});
			}
			else
			{
				Console.WriteLine("Authentication skipped because API key was not provided");
			}

			return client;
		}

		protected override async Task<IWebSocket> OnGetPositionsWebSocketAsync(Action<ExchangePosition> callback)
		{
			var client = await ConnectAndAuthenticate();
			client.AddMessageHandler<object>("order",
				msg => callback(ParsePosition(msg))
				);
			await Subscribe(client,channels);

			return client;
		}

		private ExchangePosition ParsePosition(object msg)
		{
			throw new NotImplementedException();
		}

		static async Task Authenticate(SocketClient client, string apiKey, string apiSecret)
		{
			var result = await client.Authenticate(apiKey, apiSecret);
			if (result.Success)
			{
				Console.WriteLine("Authenticated");
			}
			else
			{
				Console.WriteLine($"Authentication failed: {result.ErrorCode}");
			}
		}

		static async Task Subscribe(SocketClient client, string[] channels)
		{
			client.SetHeartbeatHandler(() => Console.WriteLine("<heartbeat>"));

			var response = await client.Subscribe(channels);
			for (int i = 0; i < channels.Length; i++)
			{
				Console.WriteLine(response[i].Success ? $"{channels[i]}: Success" : $"{channels[i]}: {response[i].ErrorCode}");
			}
		}
#endif

	}


	public class SocketResponse
	{
		public bool Success { get; set; }
		public string ErrorCode { get; set; }
	}


	public class SocketClient : IWebSocket
	{
		private string _url;
		private HubConnection _hubConnection;
		private IHubProxy _hubProxy;

		public event WebSocketConnectionDelegate Connected;
		public event WebSocketConnectionDelegate Disconnected;

		public TimeSpan ConnectInterval { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
		public TimeSpan KeepAlive { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public SocketClient(string url)
		{
			_url = url;
			_hubConnection = new HubConnection(_url);
			_hubProxy = _hubConnection.CreateHubProxy("c3");
		}

		public async Task<bool> Connect()
		{
			await _hubConnection.Start();
			return _hubConnection.State == ConnectionState.Connected;
		}

		public async Task<SocketResponse> Authenticate(string apiKey, string apiKeySecret)
		{
			var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var randomContent = $"{ Guid.NewGuid() }";
			var content = string.Join("", timestamp, randomContent);
			var signedContent = CreateSignature(apiKeySecret, content);
			var result = await _hubProxy.Invoke<SocketResponse>(
				"Authenticate",
				apiKey,
				timestamp,
				randomContent,
				signedContent);
			return result;
		}

		public IDisposable AddMessageHandler<Tmessage>(string messageName, Action<Tmessage> handler)
		{
			return _hubProxy.On(messageName, message =>
			{
				var decoded = DataConverter.Decode<Tmessage>(message);
				handler(decoded);
			});
		}

		public void SetHeartbeatHandler(Action handler)
		{
			_hubProxy.On("heartbeat", handler);
		}

		public void SetAuthExpiringHandler(Action handler)
		{
			_hubProxy.On("authenticationExpiring", handler);
		}

		private static string CreateSignature(string apiSecret, string data)
		{
			var hmacSha512 = new HMACSHA512(Encoding.ASCII.GetBytes(apiSecret));
			var hash = hmacSha512.ComputeHash(Encoding.ASCII.GetBytes(data));
			return BitConverter.ToString(hash).Replace("-", string.Empty);
		}

		public async Task<List<SocketResponse>> Subscribe(string[] channels)
		{
			try
			{
				var result = await _hubProxy.Invoke<List<SocketResponse>>("Subscribe", (object)channels);
				return result;
			}
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				throw;
			}

		}

		public Task<bool> SendMessageAsync(object message)
		{
			throw new NotImplementedException();
		}

		public void Dispose()
		{
			throw new NotImplementedException();
		}
	}

	public static class DataConverter
	{
		private static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
		{
			ContractResolver = new CamelCasePropertyNamesContractResolver(),
			DateFormatHandling = DateFormatHandling.IsoDateFormat,
			DateTimeZoneHandling = DateTimeZoneHandling.Utc,
			FloatParseHandling = FloatParseHandling.Decimal,
			MissingMemberHandling = MissingMemberHandling.Ignore,
			NullValueHandling = NullValueHandling.Ignore,
			Converters = new List<JsonConverter>
			{
				new StringEnumConverter(),
			}
		};

		public static T Decode<T>(string wireData)
		{
			// Step 1: Base64 decode the wire data into a gzip blob
			byte[] gzipData = Convert.FromBase64String(wireData);

			// Step 2: Decompress gzip blob into JSON
			string json = null;

			using (var decompressedStream = new MemoryStream())
			using (var compressedStream = new MemoryStream(gzipData))
			using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
			{
				deflateStream.CopyTo(decompressedStream);
				decompressedStream.Position = 0;
				using (var streamReader = new StreamReader(decompressedStream))
				{
					json = streamReader.ReadToEnd();
				}
			}

			// Step 3: Deserialize the JSON string into a strongly-typed object
			return JsonConvert.DeserializeObject<T>(json, _jsonSerializerSettings);
		}
	}
}
