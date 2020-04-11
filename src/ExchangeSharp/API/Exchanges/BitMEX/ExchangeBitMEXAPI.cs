/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeBitMEXAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://www.bitmex.com/api/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://www.bitmex.com/realtime";
        //public override string BaseUrl { get; set; } = "https://testnet.bitmex.com/api/v1";
        //public override string BaseUrlWebSocket { get; set; } = "wss://testnet.bitmex.com/realtime";

        private SortedDictionary<long, decimal> dict_long_decimal = new SortedDictionary<long, decimal>();
        private SortedDictionary<decimal, long> dict_decimal_long = new SortedDictionary<decimal, long>();

        public ExchangeBitMEXAPI()
        {
            RequestWindow = TimeSpan.Zero;
            NonceStyle = NonceStyle.ExpiresUnixSeconds;

            // make the nonce go 60 seconds into the future (the offset is subtracted)
            // this will give us an api-expires 60 seconds into the future
            NonceOffset = TimeSpan.FromSeconds(-60.0);

            MarketSymbolSeparator = string.Empty;
            RequestContentType = "application/json";
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;

            RateLimit = new RateGate(300, TimeSpan.FromMinutes(5));
        }

        public override Task<string> ExchangeMarketSymbolToGlobalMarketSymbolAsync(string marketSymbol)
        {
            throw new NotImplementedException();
        }

        public override Task<string> GlobalMarketSymbolToExchangeMarketSymbolAsync(string marketSymbol)
        {
            throw new NotImplementedException();
        }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if (CanMakeAuthenticatedRequest(payload))
            {
                // convert nonce to long, trim off milliseconds
                var nonce = payload["nonce"].ConvertInvariant<long>();
                payload.Remove("nonce");
                var msg = CryptoUtility.GetJsonForPayload(payload);
                var sign = $"{request.Method}{request.RequestUri.AbsolutePath}{request.RequestUri.Query}{nonce}{msg}";
                string signature = CryptoUtility.SHA256Sign(sign, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));

                request.AddHeader("api-expires", nonce.ToStringInvariant());
                request.AddHeader("api-key", PublicApiKey.ToUnsecureString());
                request.AddHeader("api-signature", signature);

                await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
            }
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            var m = await GetMarketSymbolsMetadataAsync();
            return m.Select(x => x.MarketSymbol);
        }


        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            /*
             {{
  "symbol": ".XRPXBT",
  "rootSymbol": "XRP",
  "state": "Unlisted",
  "typ": "MRCXXX",
  "listing": null,
  "front": null,
  "expiry": null,
  "settle": null,
  "relistInterval": null,
  "inverseLeg": "",
  "sellLeg": "",
  "buyLeg": "",
  "optionStrikePcnt": null,
  "optionStrikeRound": null,
  "optionStrikePrice": null,
  "optionMultiplier": null,
  "positionCurrency": "",
  "underlying": "XRP",
  "quoteCurrency": "XBT",
  "underlyingSymbol": "XRPXBT=",
  "reference": "PLNX",
  "referenceSymbol": "BTC_XRP",
  "calcInterval": null,
  "publishInterval": "2000-01-01T00:01:00Z",
  "publishTime": null,
  "maxOrderQty": null,
  "maxPrice": null,
  "lotSize": null,
  "tickSize": 1E-08,
  "multiplier": null,
  "settlCurrency": "",
  "underlyingToPositionMultiplier": null,
  "underlyingToSettleMultiplier": null,
  "quoteToSettleMultiplier": null,
  "isQuanto": false,
  "isInverse": false,
  "initMargin": null,
  "maintMargin": null,
  "riskLimit": null,
  "riskStep": null,
  "limit": null,
  "capped": false,
  "taxed": false,
  "deleverage": false,
  "makerFee": null,
  "takerFee": null,
  "settlementFee": null,
  "insuranceFee": null,
  "fundingBaseSymbol": "",
  "fundingQuoteSymbol": "",
  "fundingPremiumSymbol": "",
  "fundingTimestamp": null,
  "fundingInterval": null,
  "fundingRate": null,
  "indicativeFundingRate": null,
  "rebalanceTimestamp": null,
  "rebalanceInterval": null,
  "openingTimestamp": null,
  "closingTimestamp": null,
  "sessionInterval": null,
  "prevClosePrice": null,
  "limitDownPrice": null,
  "limitUpPrice": null,
  "bankruptLimitDownPrice": null,
  "bankruptLimitUpPrice": null,
  "prevTotalVolume": null,
  "totalVolume": null,
  "volume": null,
  "volume24h": null,
  "prevTotalTurnover": null,
  "totalTurnover": null,
  "turnover": null,
  "turnover24h": null,
  "prevPrice24h": 7.425E-05,
  "vwap": null,
  "highPrice": null,
  "lowPrice": null,
  "lastPrice": 7.364E-05,
  "lastPriceProtected": null,
  "lastTickDirection": "MinusTick",
  "lastChangePcnt": -0.0082,
  "bidPrice": null,
  "midPrice": null,
  "askPrice": null,
  "impactBidPrice": null,
  "impactMidPrice": null,
  "impactAskPrice": null,
  "hasLiquidity": false,
  "openInterest": 0,
  "openValue": 0,
  "fairMethod": "",
  "fairBasisRate": null,
  "fairBasis": null,
  "fairPrice": null,
  "markMethod": "LastPrice",
  "markPrice": 7.364E-05,
  "indicativeTaxRate": null,
  "indicativeSettlePrice": null,
  "optionUnderlyingPrice": null,
  "settledPrice": null,
  "timestamp": "2018-07-05T13:27:15Z"
}}
             */

            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            JToken allSymbols = await MakeJsonRequestAsync<JToken>("/instrument?count=500&reverse=false");
			foreach (JToken marketSymbolToken in allSymbols)
            {
                var market = new ExchangeMarket
                {
                    MarketSymbol = marketSymbolToken["symbol"].ToStringUpperInvariant(),
                    IsActive = marketSymbolToken["state"].ToStringInvariant().EqualsWithOption("Open"),
                    QuoteCurrency = marketSymbolToken["quoteCurrency"].ToStringUpperInvariant(),
                    BaseCurrency = marketSymbolToken["underlying"].ToStringUpperInvariant(),
                };

                try
                {
                    market.PriceStepSize = marketSymbolToken["tickSize"].ConvertInvariant<decimal>();
                    market.MaxPrice = marketSymbolToken["maxPrice"].ConvertInvariant<decimal>();
                    //market.MinPrice = symbol["minPrice"].ConvertInvariant<decimal>();

                    market.MaxTradeSize = marketSymbolToken["maxOrderQty"].ConvertInvariant<decimal>();
                    //market.MinTradeSize = symbol["minQty"].ConvertInvariant<decimal>();
                    //market.QuantityStepSize = symbol["stepSize"].ConvertInvariant<decimal>();
                }
                catch
                {

                }
                markets.Add(market);
            }
            return markets;
        }

        protected override Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
        {
            /*
{"table":"trade","action":"partial","keys":[],
"types":{"timestamp":"timestamp","symbol":"symbol","side":"symbol","size":"long","price":"float","tickDirection":"symbol","trdMatchID":"guid","grossValue":"long","homeNotional":"float","foreignNotional":"float"},
"foreignKeys":{"symbol":"instrument","side":"side"},
"attributes":{"timestamp":"sorted","symbol":"grouped"},
"filter":{"symbol":"XBTUSD"},
"data":[{"timestamp":"2018-07-06T08:31:53.333Z","symbol":"XBTUSD","side":"Buy","size":10000,"price":6520,"tickDirection":"PlusTick","trdMatchID":"a296312f-c9a4-e066-2f9e-7f4cf2751f0a","grossValue":153370000,"homeNotional":1.5337,"foreignNotional":10000}]}
             */

            return ConnectWebSocketAsync(string.Empty, async (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

				if (token["error"] != null)
				{
					Logger.Info(token["error"].ToStringInvariant());
					return;
				}
				else if (token["table"] == null)
                {
					return;
                }

                var action = token["action"].ToStringInvariant();
                JArray data = token["data"] as JArray;
                foreach (var t in data)
                {
                    var marketSymbol = t["symbol"].ToStringInvariant();
                    await callback(new KeyValuePair<string, ExchangeTrade>(marketSymbol, t.ParseTrade("size", "price", "side", "timestamp", TimestampType.Iso8601, "trdMatchID")));
                }
            }, async (_socket) =>
            {
                if (marketSymbols == null || marketSymbols.Length == 0)
                {
					await _socket.SendMessageAsync(new { op = "subscribe", args = "trade" });
				}
				else
				{
					await _socket.SendMessageAsync(new { op = "subscribe", args = marketSymbols.Select(s => "trade:" + this.NormalizeMarketSymbol(s)).ToArray() });
				}
            });
        }

        protected override async Task<IWebSocket> OnGetDeltaOrderBookWebSocketAsync(Action<ExchangeOrderBook> callback, int maxCount = 20, params string[] marketSymbols)
        {
            /*
{"info":"Welcome to the BitMEX Realtime API.","version":"2018-06-29T18:05:14.000Z","timestamp":"2018-07-05T14:22:26.267Z","docs":"https://www.bitmex.com/app/wsAPI","limit":{"remaining":39}}
{"success":true,"subscribe":"orderBookL2:XBTUSD","request":{"op":"subscribe","args":["orderBookL2:XBTUSD"]}}
{"table":"orderBookL2","action":"update","data":[{"symbol":"XBTUSD","id":8799343000,"side":"Buy","size":350544}]}
             */

            if (marketSymbols == null || marketSymbols.Length == 0)
            {
                marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
            }
            return await ConnectWebSocketAsync(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);

                if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }

                var action = token["action"].ToStringInvariant();
                JArray data = token["data"] as JArray;

                ExchangeOrderBook book = new ExchangeOrderBook();
                var price = 0m;
                var size = 0m;
                foreach (var d in data)
                {
                    var marketSymbol = d["symbol"].ToStringInvariant();
                    var id = d["id"].ConvertInvariant<long>();
                    if (d["price"] == null)
                    {
                        if (!dict_long_decimal.TryGetValue(id, out price))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        price = d["price"].ConvertInvariant<decimal>();
                        dict_long_decimal[id] = price;
                        dict_decimal_long[price] = id;
                    }

                    var side = d["side"].ToStringInvariant();

                    if (d["size"] == null)
                    {
                        size = 0m;
                    }
                    else
                    {
                        size = d["size"].ConvertInvariant<decimal>();
                    }

                    var depth = new ExchangeOrderPrice { Price = price, Amount = size };

                    if (side.EqualsWithOption("Buy"))
                    {
                        book.Bids[depth.Price] = depth;
                    }
                    else
                    {
                        book.Asks[depth.Price] = depth;
                    }
                    book.MarketSymbol = marketSymbol;
                }

                if (!string.IsNullOrEmpty(book.MarketSymbol))
                {
                    callback(book);
                }
                return Task.CompletedTask;
            }, async (_socket) =>
            {
                if (marketSymbols.Length == 0)
                {
                    marketSymbols = (await GetMarketSymbolsAsync()).ToArray();
                }
                await _socket.SendMessageAsync(new { op = "subscribe", args = marketSymbols.Select(s => "orderBookL2:" + this.NormalizeMarketSymbol(s)).ToArray() });
            });
        }

        public async Task<IWebSocket> GetPositionWebSocketAsync(Action<ExchangePosition> callback)
        {
            /*
"{\"info\":\"Welcome to the BitMEX Realtime API.\",\"version\":\"2020-04-08T01:10:16.000Z\",\"timestamp\":\"2020-04-11T11:43:31.856Z\",\"docs\":\"https://testnet.bitmex.com/app/wsAPI\",\"limit\":{\"remaining\":39}}"
"{\"success\":true,\"request\":{\"op\":\"authKeyExpires\",\"args\":[\"Foo123\",1586605471,\"Bar456\"]}}"
"{\"success\":true,\"subscribe\":\"position\",\"request\":{\"op\":\"subscribe\",\"args\":\"position\"}}"
"{\"table\":\"position\",\"action\":\"partial\",\"keys\":[\"account\",\"symbol\"],\"types\":{\"account\":\"long\",\"symbol\":\"symbol\",\"currency\":\"symbol\",\"underlying\":\"symbol\",\"quoteCurrency\":\"symbol\",\"commission\":\"float\",\"initMarginReq\":\"float\",\"maintMarginReq\":\"float\",\"riskLimit\":\"long\",\"leverage\":\"float\",\"crossMargin\":\"boolean\",\"deleveragePercentile\":\"float\",\"rebalancedPnl\":\"long\",\"prevRealisedPnl\":\"long\",\"prevUnrealisedPnl\":\"long\",\"prevClosePrice\":\"float\",\"openingTimestamp\":\"timestamp\",\"openingQty\":\"long\",\"openingCost\":\"long\",\"openingComm\":\"long\",\"openOrderBuyQty\":\"long\",\"openOrderBuyCost\":\"long\",\"openOrderBuyPremium\":\"long\",\"openOrderSellQty\":\"long\",\"openOrderSellCost\":\"long\",\"openOrderSellPremium\":\"long\",\"execBuyQty\":\"long\",\"execBuyCost\":\"long\",\"execSellQty\":\"long\",\"execSellCost\":\"long\",\"execQty\":\"long\",\"execCost\":\"long\",\"execComm\":\"long\",\"currentTimestamp\":\"timestamp\",\"currentQty\":\"long\",\"currentCost\":\"long\",\"currentComm\":\"long\",\"realisedCost\":\"long\",\"unrealisedCost\":\"long\",\"grossOpenCost\":\"long\",\"grossOpenPremium\":\"long\",\"grossExecCost\":\"long\",\"isOpen\":\"boolean\",\"markPrice\":\"float\",\"markValue\":\"long\",\"riskValue\":\"long\",\"homeNotional\":\"float\",\"foreignNotional\":\"float\",\"posState\":\"symbol\",\"posCost\":\"long\",\"posCost2\":\"long\",\"posCross\":\"long\",\"posInit\":\"long\",\"posComm\":\"long\",\"posLoss\":\"long\",\"posMargin\":\"long\",\"posMaint\":\"long\",\"posAllowance\":\"long\",\"taxableMargin\":\"long\",\"initMargin\":\"long\",\"maintMargin\":\"long\",\"sessionMargin\":\"long\",\"targetExcessMargin\":\"long\",\"varMargin\":\"long\",\"realisedGrossPnl\":\"long\",\"realisedTax\":\"long\",\"realisedPnl\":\"long\",\"unrealisedGrossPnl\":\"long\",\"longBankrupt\":\"long\",\"shortBankrupt\":\"long\",\"taxBase\":\"long\",\"indicativeTaxRate\":\"float\",\"indicativeTax\":\"long\",\"unrealisedTax\":\"long\",\"unrealisedPnl\":\"long\",\"unrealisedPnlPcnt\":\"float\",\"unrealisedRoePcnt\":\"float\",\"simpleQty\":\"float\",\"simpleCost\":\"float\",\"simpleValue\":\"float\",\"simplePnl\":\"float\",\"simplePnlPcnt\":\"float\",\"avgCostPrice\":\"float\",\"avgEntryPrice\":\"float\",\"breakEvenPrice\":\"float\",\"marginCallPrice\":\"float\",\"liquidationPrice\":\"float\",\"bankruptPrice\":\"float\",\"timestamp\":\"timestamp\",\"lastPrice\":\"float\",\"lastValue\":\"long\"},\"foreignKeys\":{\"symbol\":\"instrument\"},\"attributes\":{\"account\":\"sorted\",\"symbol\":\"grouped\",\"underlying\":\"grouped\"},\"filter\":{\"account\":12345678},\"data\":[{\"account\":12345678,\"symbol\":\"XBTUSD\",\"currency\":\"XBt\",\"underlying\":\"XBT\",\"quoteCurrency\":\"USD\",\"commission\":0.00075,\"initMarginReq\":0.01,\"maintMarginReq\":0.005,\"riskLimit\":20000000000,\"leverage\":100,\"crossMargin\":true,\"deleveragePercentile\":null,\"rebalancedPnl\":1234,\"prevRealisedPnl\":678,\"prevUnrealisedPnl\":0,\"prevClosePrice\":6905.23,\"openingTimestamp\":\"2020-04-11T11:00:00.000Z\",\"openingQty\":0,\"openingCost\":9876,\"openingComm\":6543,\"openOrderBuyQty\":0,\"openOrderBuyCost\":0,\"openOrderBuyPremium\":0,\"openOrderSellQty\":0,\"openOrderSellCost\":0,\"openOrderSellPremium\":0,\"execBuyQty\":0,\"execBuyCost\":0,\"execSellQty\":0,\"execSellCost\":0,\"execQty\":0,\"execCost\":0,\"execComm\":0,\"currentTimestamp\":\"2020-04-11T11:00:00.330Z\",\"currentQty\":0,\"currentCost\":8765,\"currentComm\":564542,\"realisedCost\":9876,\"unrealisedCost\":0,\"grossOpenCost\":0,\"grossOpenPremium\":0,\"grossExecCost\":0,\"isOpen\":false,\"markPrice\":null,\"markValue\":0,\"riskValue\":0,\"homeNotional\":0,\"foreignNotional\":0,\"posState\":\"\",\"posCost\":0,\"posCost2\":0,\"posCross\":0,\"posInit\":0,\"posComm\":0,\"posLoss\":0,\"posMargin\":0,\"posMaint\":0,\"posAllowance\":0,\"taxableMargin\":0,\"initMargin\":0,\"maintMargin\":0,\"sessionMargin\":0,\"targetExcessMargin\":0,\"varMargin\":0,\"realisedGrossPnl\":-7654,\"realisedTax\":0,\"realisedPnl\":-7654,\"unrealisedGrossPnl\":0,\"longBankrupt\":0,\"shortBankrupt\":0,\"taxBase\":0,\"indicativeTaxRate\":null,\"indicativeTax\":0,\"unrealisedTax\":0,\"unrealisedPnl\":0,\"unrealisedPnlPcnt\":0,\"unrealisedRoePcnt\":0,\"simpleQty\":null,\"simpleCost\":null,\"simpleValue\":null,\"simplePnl\":null,\"simplePnlPcnt\":null,\"avgCostPrice\":null,\"avgEntryPrice\":null,\"breakEvenPrice\":null,\"marginCallPrice\":null,\"liquidationPrice\":null,\"bankruptPrice\":null,\"timestamp\":\"2020-04-11T11:00:00.330Z\",\"lastPrice\":null,\"lastValue\":0}]}"
            */

            return await ConnectWebSocketAsync(string.Empty, (_socket, msg) =>
            {
                var str = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(str);
				if (token["error"] != null)
				{
					Logger.Info(token["error"].ToStringInvariant());
                    return Task.CompletedTask;
				}
				else if (token["table"] == null)
                {
                    return Task.CompletedTask;
                }

                JArray data = token["data"] as JArray;
                foreach (var d in data)
                {
                    var position = ParsePosition(d);
                    callback(position);
                }
                return Task.CompletedTask;

            }, async (_socket) =>
            {
				long nonce = (await GenerateNonceAsync()).ConvertInvariant<long>();
                var authPayload = $"GET/realtime{nonce}";
                string signature = CryptoUtility.SHA256Sign(authPayload, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));

                var authArgs = new object[]{PublicApiKey.ToUnsecureString(), nonce, signature};
                await _socket.SendMessageAsync(new { op = "authKeyExpires", args = authArgs });
                await _socket.SendMessageAsync(new { op = "subscribe", args = "position" });
            });
        }
        

        protected override async Task<IEnumerable<MarketCandle>> OnGetCandlesAsync(string marketSymbol, int periodSeconds, DateTime? startDate = null, DateTime? endDate = null, int? limit = null)
        {
            /*
             [
{"timestamp":"2017-01-01T00:00:00.000Z","symbol":"XBTUSD","open":968.29,"high":968.29,"low":968.29,"close":968.29,"trades":0,"volume":0,"vwap":null,"lastSize":null,"turnover":0,"homeNotional":0,"foreignNotional":0},
{"timestamp":"2017-01-01T00:01:00.000Z","symbol":"XBTUSD","open":968.29,"high":968.76,"low":968.49,"close":968.7,"trades":17,"volume":12993,"vwap":968.72,"lastSize":2000,"turnover":1341256747,"homeNotional":13.412567469999997,"foreignNotional":12993},
             */

            List<MarketCandle> candles = new List<MarketCandle>();
            string periodString = PeriodSecondsToString(periodSeconds);
            string url = $"/trade/bucketed?binSize={periodString}&partial=false&symbol={marketSymbol}&reverse=true" + marketSymbol;
            if (startDate != null)
            {
                url += "&startTime=" + startDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");
            }
            if (endDate != null)
            {
                url += "&endTime=" + endDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");
            }
            if (limit != null)
            {
                url += "&count=" + (limit.Value.ToStringInvariant());
            }

            var obj = await MakeJsonRequestAsync<JToken>(url);
            foreach (var t in obj)
            {
                candles.Add(this.ParseCandle(t, marketSymbol, periodSeconds, "open", "high", "low", "close", "timestamp", TimestampType.Iso8601, "volume", "turnover", "vwap"));
            }
            candles.Reverse();

            return candles;
        }

        public async Task<IEnumerable<ExchangeTrade>> GetHistoricalTradesAsync(
            string marketSymbol = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int? startingIndex = null,
            int? limit = 1000)
        {
            List<ExchangeTrade> trades = new List<ExchangeTrade>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string url = "/trade?";
            url += "&columns=[\"symbol\", \"size\", \"price\", \"side\", \"timestamp\", \"trdMatchID\"]";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                url += "&symbol=" + NormalizeMarketSymbol(marketSymbol);
            }
            if (startDate != null)
            {
                url += "&startTime=" + startDate.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "Z";
            }
            if (endDate != null)
            {
                url += "&endTime=" + endDate.Value.ToString("yyyy-MM-ddTHH:mm:ss.fff") + "Z";
            }
            if (limit != null)
            {
                url += "&count=" + (limit.Value.ToStringInvariant());
            }
            if (startingIndex != null)
            {
                url += "&start=" + (startingIndex.Value.ToStringInvariant());
            }

            var obj = await MakeJsonRequestAsync<JToken>(url);
            foreach (var t in obj)
            {
                trades.Add(t.ParseTrade("size", "price", "side", "timestamp", TimestampType.Iso8601, "trdMatchID"));
            }

            return trades;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            /*
{[
  {
    "account": 93592,
    "currency": "XBt",
    "riskLimit": 1000000000000,
    "prevState": "",
    "state": "",
    "action": "",
    "amount": 141755795,
    "pendingCredit": 0,
    "pendingDebit": 0,
    "confirmedDebit": 0,
    "prevRealisedPnl": 0,
    "prevUnrealisedPnl": 0,
    "grossComm": 0,
    "grossOpenCost": 0,
    "grossOpenPremium": 0,
    "grossExecCost": 0,
    "grossMarkValue": 0,
    "riskValue": 0,
    "taxableMargin": 0,
    "initMargin": 0,
    "maintMargin": 0,
    "sessionMargin": 0,
    "targetExcessMargin": 0,
    "varMargin": 0,
    "realisedPnl": 0,
    "unrealisedPnl": 0,
    "indicativeTax": 0,
    "unrealisedProfit": 0,
    "syntheticMargin": 0,
    "walletBalance": 141755795,
    "marginBalance": 141755795,
    "marginBalancePcnt": 1,
    "marginLeverage": 0,
    "marginUsedPcnt": 0,
    "excessMargin": 141755795,
    "excessMarginPcnt": 1,
    "availableMargin": 141755795,
    "withdrawableMargin": 141755795,
    "timestamp": "2018-07-08T07:40:24.395Z",
    "grossLastValue": 0,
    "commission": null
  }
]}
             */


            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/margin?currency=all", BaseUrl, payload);
            foreach (var item in token)
            {
                var balance = item["marginBalance"].ConvertInvariant<decimal>();
                var currency = item["currency"].ToStringInvariant();

                if (amounts.ContainsKey(currency))
                {
                    amounts[currency] += balance;
                }
                else
                {
                    amounts[currency] = balance;
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var payload = await GetNoncePayloadAsync();
            JToken token = await MakeJsonRequestAsync<JToken>($"/user/margin?currency=all", BaseUrl, payload);
            foreach (var item in token)
            {
                var balance = item["availableMargin"].ConvertInvariant<decimal>();
                var currency = item["currency"].ToStringInvariant();

                if (amounts.ContainsKey(currency))
                {
                    amounts[currency] += balance;
                }
                else
                {
                    amounts[currency] = balance;
                }
            }
            return amounts;
        }

        public async Task<IEnumerable<ExchangePosition>> GetCurrentPositionsAsync()
        {
            var payload = await GetNoncePayloadAsync();
            string url = "/position?";
            url += "&columns=[\"symbol\", \"currentQty\", \"avgEntryPrice\", \"liquidationPrice\", \"leverage\", \"lastPrice\", \"currentTimestamp\"]";
            JToken token = await MakeJsonRequestAsync<JToken>(url, BaseUrl, payload);
            List<ExchangePosition> positions = new List<ExchangePosition>();
            foreach (var item in token)
            {
                positions.Add(ParsePosition(item));
            }
            return positions;
        }

        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            //string query = "/order";
            string query = "/order?filter={\"open\": true}";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                query += "&symbol=" + NormalizeMarketSymbol(marketSymbol);
            }
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
            }

            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string query = $"/order?filter={{\"orderID\": \"{orderId}\"}}";
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "GET");
            foreach (JToken order in token)
            {
                orders.Add(ParseOrder(order));
            }

            return orders[0];
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["orderID"] = orderId;
            JToken token = await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "DELETE");
        }
    
        public async Task CancelAllOrdersAsync(string marketSymbol = null)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            string query = "/order/all";
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                payload["symbol"] = NormalizeMarketSymbol(marketSymbol);
            }
            JToken token = await MakeJsonRequestAsync<JToken>(query, BaseUrl, payload, "DELETE");
        }

        public async Task DeadmanAsync(int timeoutMS)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            payload["timeout"] = timeoutMS;
            JToken token = await MakeJsonRequestAsync<JToken>("/order/cancelAllAfter", BaseUrl, payload, "POST");
        }
        
        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            AddOrderToPayload(order, payload);
            JToken token = await MakeJsonRequestAsync<JToken>("/order", BaseUrl, payload, "POST");
            return ParseOrder(token);
        }

        private async Task<ExchangeOrderResult[]> PlaceOrdersAsync(string requestMethod, params ExchangeOrderRequest[] orders)
        {
            List<ExchangeOrderResult> results = new List<ExchangeOrderResult>();
            Dictionary<string, object> payload = await GetNoncePayloadAsync();
            List<Dictionary<string, object>> orderRequests = new List<Dictionary<string, object>>();
            foreach (ExchangeOrderRequest order in orders)
            {
                Dictionary<string, object> subPayload = new Dictionary<string, object>();
                AddOrderToPayload(order, subPayload);
                orderRequests.Add(subPayload);
            }
            payload["orders"] = orderRequests;
            JToken token = await MakeJsonRequestAsync<JToken>("/order/bulk", BaseUrl, payload, requestMethod);
            foreach (JToken orderResultToken in token)
            {
                results.Add(ParseOrder(orderResultToken));
            }
            return results.ToArray();
        }

        protected override async Task<ExchangeOrderResult[]> OnPlaceOrdersAsync(params ExchangeOrderRequest[] orders)
        {
            return await PlaceOrdersAsync("POST", orders);
        }

        public async Task<ExchangeOrderResult[]> AmendOrdersAsync(params ExchangeOrderRequest[] orders)
        {
            return await PlaceOrdersAsync("PUT", orders);
        }

        private void AddOrderToPayload(ExchangeOrderRequest order, Dictionary<string, object> payload)
        {
            payload["symbol"] = order.MarketSymbol;
            payload["ordType"] = order.OrderType.ToStringInvariant();
            payload["side"] = order.IsBuy ? "Buy" : "Sell";
            payload["orderQty"] = order.Amount;

            if(order.OrderId != null)
                payload["orderID"] = order.OrderId;

            if(order.ClientOrderId != null)
                payload["clOrdID"] = order.ClientOrderId;

            if(order.OrderType!=OrderType.Market)
                payload["price"] = order.Price;

            if (order.ExtraParameters.TryGetValue("execInst", out var execInst))
            {
                payload["execInst"] = execInst;
            }
        }

        private ExchangePosition ParsePosition(JToken token)
        {
            ExchangePosition result = new ExchangePosition
            {
                MarketSymbol = token["symbol"].ToStringUpperInvariant(),
                Amount = token["currentQty"].ConvertInvariant<decimal>(),
                AveragePrice = token["avgEntryPrice"].ConvertInvariant<decimal>(),
                LiquidationPrice = token["liquidationPrice"].ConvertInvariant<decimal>(),
                Leverage = token["leverage"].ConvertInvariant<decimal>(),
                LastPrice = token["lastPrice"].ConvertInvariant<decimal>(),
                TimeStamp = CryptoUtility.ParseTimestamp(token["currentTimestamp"], TimestampType.Iso8601)
            };
            return result;
        }

        private ExchangeOrderResult ParseOrder(JToken token)
        {
            /*
{[
  {
    "orderID": "b7b8518a-c0d8-028d-bb6e-d843f8f723a3",
    "clOrdID": "",
    "clOrdLinkID": "",
    "account": 93592,
    "symbol": "XBTUSD",
    "side": "Buy",
    "simpleOrderQty": null,
    "orderQty": 1,
    "price": 5500,
    "displayQty": null,
    "stopPx": null,
    "pegOffsetValue": null,
    "pegPriceType": "",
    "currency": "USD",
    "settlCurrency": "XBt",
    "ordType": "Limit",
    "timeInForce": "GoodTillCancel",
    "execInst": "ParticipateDoNotInitiate",
    "contingencyType": "",
    "exDestination": "XBME",
    "ordStatus": "Canceled",
    "triggered": "",
    "workingIndicator": false,
    "ordRejReason": "",
    "simpleLeavesQty": 0,
    "leavesQty": 0,
    "simpleCumQty": 0,
    "cumQty": 0,
    "avgPx": null,
    "multiLegReportingType": "SingleSecurity",
    "text": "Canceled: Canceled via API.\nSubmission from testnet.bitmex.com",
    "transactTime": "2018-07-08T09:20:39.428Z",
    "timestamp": "2018-07-08T11:35:05.334Z"
  }
]}
            */
            ExchangeOrderResult result = new ExchangeOrderResult
            {
                Amount = token["orderQty"].ConvertInvariant<decimal>(),
                AmountFilled = token["cumQty"].ConvertInvariant<decimal>(),
                Price = token["price"].ConvertInvariant<decimal>(),
                IsBuy = token["side"].ToStringInvariant().EqualsWithOption("Buy"),
                OrderDate = token["transactTime"].ConvertInvariant<DateTime>(),
                OrderId = token["orderID"].ToStringInvariant(),
                ClientOrderId = token["clOrdID"].ToStringInvariant(),
                MarketSymbol = token["symbol"].ToStringInvariant()
            };

            // http://www.onixs.biz/fix-dictionary/5.0.SP2/tagNum_39.html
            switch (token["ordStatus"].ToStringInvariant())
            {
                case "New":
                    result.Result = ExchangeAPIOrderResult.Pending;
                    break;
                case "PartiallyFilled":
                    result.Result = ExchangeAPIOrderResult.FilledPartially;
                    break;
                case "Filled":
                    result.Result = ExchangeAPIOrderResult.Filled;
                    break;
                case "Canceled":
                    result.Result = ExchangeAPIOrderResult.Canceled;
                    break;

                default:
                    result.Result = ExchangeAPIOrderResult.Error;
                    break;
            }

            return result;
        }


        //private decimal GetInstrumentTickSize(ExchangeMarket market)
        //{
        //    if (market.MarketName == "XBTUSD")
        //    {
        //        return 0.01m;
        //    }
        //    return market.PriceStepSize.Value;
        //}

        //private ExchangeMarket GetMarket(string symbol)
        //{
        //    var m = GetSymbolsMetadata();
        //    return m.Where(x => x.MarketName == symbol).First();
        //}

        //private decimal GetPriceFromID(long id, ExchangeMarket market)
        //{
        //    return ((100000000L * market.Idx) - id) * GetInstrumentTickSize(market);
        //}

        //private long GetIDFromPrice(decimal price, ExchangeMarket market)
        //{
        //    return (long)((100000000L * market.Idx) - (price / GetInstrumentTickSize(market)));
        //}
    }

    public partial class ExchangeName { public const string BitMEX = "BitMEX"; }
}
