using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeCoincheckAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://coincheck.com";
		public override string BaseUrlWebSocket { get; set; } = "wss://ws-api.coincheck.com";

		public ExchangeCoincheckAPI()
		{
			NonceStyle = NonceStyle.UnixSeconds;
			NonceOffset = TimeSpan.FromSeconds(0.1);
			// WebSocketOrderBookType = not implemented
			MarketSymbolSeparator = "_";
			MarketSymbolIsUppercase = false;
			// ExchangeGlobalCurrencyReplacements[] not implemented
		}

		protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
		{ // unclear, but appears like this is all they have available, at least for trade stream (from their poor documentation)
			return new[] { "btc_jpy", "etc_jpy", "fct_jpy", "mona_jpy", "plt_jpy", };
		}

		protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(
				Func<KeyValuePair<string, ExchangeTrade>, Task> callback,
				params string[] marketSymbols
		)
		{
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
			}
			return await ConnectPublicWebSocketAsync(
					"",
					async (_socket, msg) =>
					{ /*[
				  2357062,		// 0 "ID",
				  "[pair]",		// 1 "Currency pair"
				  "148638.0",	// 2 "Order rate"
				  "5.0",		// 3 "Order amount"
				  "sell"		// 4 "Specify order_type."
				]*/
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						// no error msgs provided
						if (token.Type == JTokenType.Array)
						{
							var trade = token.ParseTrade(3, 2, 4, null, TimestampType.None, 0);
							string marketSymbol = token[1].ToStringInvariant();
							await callback(
												new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
										);
						}
						else
							Logger.Warn($"Unexpected token type {token.Type}");
					},
					async (_socket) =>
					{ /*{
			   	"type": "subscribe",
					"channel": "[pair]-trades"
				}*/
						foreach (var marketSymbol in marketSymbols)
						{
							var subscribeRequest = new
							{
								type = "subscribe",
								channel = $"{marketSymbol}-trades",
							};
							await _socket.SendMessageAsync(subscribeRequest);
						}
					}
			);
		}
	}

	public partial class ExchangeName
	{
		public const string Coincheck = "Coincheck";
	}
}
