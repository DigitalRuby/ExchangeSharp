using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
	public sealed partial class ExchangeUPbitAPI : ExchangeAPI
	{
		public override string BaseUrl { get; set; } = "https://api.upbit.com";
		public override string BaseUrlWebSocket { get; set; } = "wss://api.upbit.com/websocket/v1";

		public ExchangeUPbitAPI()
		{
			//NonceStyle = NonceStyle.Iso8601; unclear what this is
			NonceOffset = TimeSpan.FromSeconds(0.1);
			// WebSocketOrderBookType = not implemented
			MarketSymbolSeparator = "-";
			MarketSymbolIsUppercase = true;
			// ExchangeGlobalCurrencyReplacements[] not implemented
		}

		public override async Task<IEnumerable<string>> GetMarketSymbolsAsync()
		{ /*[
				{
					"market": "KRW-BTC",
					"korean_name": "비트코인",
					"english_name": "Bitcoin"
				},
				...
			] */
			var instruments = await MakeJsonRequestAsync<JToken>("v1/market/all");
			var markets = new List<ExchangeMarket>();
			foreach (JToken instrument in instruments)
			{
				markets.Add(
						new ExchangeMarket
						{
							MarketSymbol = instrument["market"].ToStringInvariant(),
							AltMarketSymbol = instrument["korean_name"].ToStringInvariant(),
							MarketId = instrument["english_name"].ToStringInvariant(),
						}
				);
			}
			return markets.Select(m => m.MarketSymbol);
		}

		public override async Task<IWebSocket> GetTradesWebSocketAsync(
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
					{
						/*{"mk":"KRW-BTC","tms":1523531768829,"td":"2018-04-12","ttm":"11:16:03","ttms":1523531763000,"tp":7691000.0,"tv":0.00996719,"ab":"BID","pcp":7429000.00000000,"c":"RISE","cp":262000.00000000,"sid":1523531768829000,"st":"SNAPSHOT"}
						 {"mk":"BTC-BCH","tms":1523531745481,"td":"2018-04-12","ttm":"11:15:48","ttms":1523531748370,"tp":0.09601999,"tv":0.18711789,"ab":"BID","pcp":0.09618000,"c":"FALL","cp":0.00016001,"sid":15235317454810000,"st":"SNAPSHOT"}
						 {"mk":"KRW-BTC","tms":1523531769250,"td":"2018-04-12","ttm":"11:16:04","ttms":1523531764000,"tp":7691000.0,"tv":0.07580113,"ab":"BID","pcp":7429000.00000000,"c":"RISE","cp":262000.00000000,"sid":1523531769250000,"st":"REALTIME"}*/
						JToken token = JToken.Parse(msg.ToStringFromUTF8());
						// UPbit does not provide an error element
						if (token["ty"].ToStringInvariant() == "trade")
						{
							var trade = token.ParseTrade(
												"tv",
												"tp",
												"ab",
												"trade_timestamp",
												TimestampType.UnixMilliseconds,
												"sid",
												"BID"
										);
							string marketSymbol = token["cd"].ToStringInvariant();
							if (token["st"].ToStringInvariant() == "SNAPSHOT")
							{
								trade.Flags |= ExchangeTradeFlags.IsFromSnapshot;
							}
							else if (token["st"].ToStringInvariant() == "REALTIME") { }
							else
								Logger.Warn($"Unknown stream type {token["st"].ToStringInvariant()}");
							await callback(
												new KeyValuePair<string, ExchangeTrade>(marketSymbol, trade)
										);
						}
						else
							Logger.Warn($"Unknown response type {token["ty"].ToStringInvariant()}");
					},
					async (_socket) =>
					{ // [{"ticket":"test"},{"type":"trade","codes":["KRW-BTC","BTC-BCH"]},{"format":"SIMPLE"}]
						//var subscribeRequest = new[] { new
						//{
						//	ticket = "exchangeTrades",
						//		type = "trade",
						//		codes = marketSymbols,
						//	format = "SIMPLE",
						//} };
						var subscribeRequest =
											@"[{""ticket"":""test""},{""type"":""trade"",""codes"":["
											+ String.Join(",", marketSymbols.Select(s => $@"""{s}"""))
											+ @"]},{""format"":""SIMPLE""}]";
						await _socket.SendMessageAsync(subscribeRequest);
					}
			);
		}
	}

	public partial class ExchangeName
	{
		public const string UPbit = "UPbit";
	}
}
