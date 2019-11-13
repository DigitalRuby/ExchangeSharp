using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ExchangeSharp.BL3P
{
	internal sealed class MultiWebsocketWrapper : IWebSocket
	{
		private readonly Dictionary<IWebSocket, WsStatus> webSockets;

		private enum WsStatus : byte
		{
			Unknown = 0,
			Connected,
			Disconnected
		}

		public MultiWebsocketWrapper(params IWebSocket[] webSockets)
		{
			this.webSockets = webSockets.ToDictionary(k => k, _ => WsStatus.Unknown);
			InstallEventListeners();
		}

		private void InstallEventListeners()
		{
			foreach (var ws in webSockets)
			{
				ws.Key.Connected += socket =>
				{
					webSockets[socket] = WsStatus.Connected;

					if (webSockets.Values.All(v => v == WsStatus.Connected))
					{
						OnConnected();
					}

					return Task.CompletedTask;
				};
				ws.Key.Disconnected += socket =>
				{
					webSockets[socket] = WsStatus.Disconnected;

					foreach (var otherWs in webSockets.Keys)
					{
						if (!socket.Equals(otherWs))
						{
							otherWs.Dispose();
						}
					}

					OnDisconnected();

					return Task.CompletedTask;
				};
			}
		}

		public void Dispose()
		{
			foreach (var webSocket in webSockets)
			{
				webSocket.Key?.Dispose();
			}
		}

		public TimeSpan ConnectInterval { get; set; }

		public TimeSpan KeepAlive { get; set; }

		public event WebSocketConnectionDelegate Connected;

		public event WebSocketConnectionDelegate Disconnected;

		public async Task<bool> SendMessageAsync(object message)
		{
			var tasks = await Task.WhenAll(webSockets.Select(ws => ws.Key.SendMessageAsync(message)))
				.ConfigureAwait(false);

			return tasks.All(r => r);
		}

		private void OnConnected()
		{
			Connected?.Invoke(this);
		}

		private void OnDisconnected()
		{
			Disconnected?.Invoke(this);
		}
	}
}
