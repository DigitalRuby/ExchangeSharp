/*
MIT LICENSE

Copyright 2020 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeBybitAPI : ExchangeAPI
    {
        private int _recvWindow = 30000;

        public override string BaseUrl { get; set; } = "https://api.bybit.com";
        public override string BaseUrlWebSocket { get; set; } = "wss://stream.bybit.com/realtime";
        // public override string BaseUrl { get; set; } = "https://api-testnet.bybit.com/";
        // public override string BaseUrlWebSocket { get; set; } = "wss://stream-testnet.bybit.com/realtime";

        public ExchangeBybitAPI()
        {
			NonceStyle = NonceStyle.UnixMilliseconds;
            NonceOffset = TimeSpan.FromSeconds(1.0);

            MarketSymbolSeparator = string.Empty;
            RequestContentType = "application/json";
            WebSocketOrderBookType = WebSocketOrderBookType.FullBookFirstThenDeltas;

            RateLimit = new RateGate(100, TimeSpan.FromMinutes(1));
        }

        public override Task<string> ExchangeMarketSymbolToGlobalMarketSymbolAsync(string marketSymbol)
        {
            throw new NotImplementedException();
        }

        public override Task<string> GlobalMarketSymbolToExchangeMarketSymbolAsync(string marketSymbol)
        {
            throw new NotImplementedException();
        }

        // Was initially struggling with 10002 timestamp errors, so tried calcing clock drift on every request.
        // Settled on positive NonceOffset so our clock is not likely ahead of theirs on arrival (assuming accurate client/server side clocks)
        // And larger recv_window so our packets have plenty of time to arrive
        // protected override async Task OnGetNonceOffset()
        // {
        //     string stringResult = await MakeRequestAsync("/v2/public/time");
        //     var token  = JsonConvert.DeserializeObject<JToken>(stringResult);
        //     DateTime serverDate = CryptoUtility.UnixTimeStampToDateTimeSeconds(token["time_now"].ConvertInvariant<Double>());
        //     var now = CryptoUtility.UtcNow;
        //     NonceOffset = now - serverDate + TimeSpan.FromSeconds(1); // how much time to substract from Nonce when making a request
        // }

        protected override async Task ProcessRequestAsync(IHttpWebRequest request, Dictionary<string, object> payload)
        {
            if ((payload != null) && payload.ContainsKey("sign") && request.Method == "POST")
            {
                await CryptoUtility.WritePayloadJsonToRequestAsync(request, payload);
            }
        }

#nullable enable
        //Not using MakeJsonRequest... so we can perform our own check on the ret_code 
        private async Task<T> DoMakeJsonRequestAsync<T>(string url, string? baseUrl = null, Dictionary<string, object>? payload = null, string? requestMethod = null)
        {
            await new SynchronizationContextRemover();

            string stringResult = await MakeRequestAsync(url, baseUrl, payload, requestMethod);
            return JsonConvert.DeserializeObject<T>(stringResult);
        }
#nullable disable

        private JToken CheckRetCode(JToken response, string[] allowedRetCodes)
        {
            var result = GetResult(response, out var retCode, out var retMessage);
            if (!allowedRetCodes.Contains(retCode))
            {
                throw new Exception($"Invalid ret_code {retCode}, ret_msg {retMessage}");
            }
            return result;
        }
   
        private JToken CheckRetCode(JToken response)
        {
            return CheckRetCode(response, new string[] {"0"});
        }

        private JToken GetResult(JToken response, out string retCode, out string retMessage)
        {
            retCode = response["ret_code"].ToStringInvariant();
            retMessage = response["ret_msg"].ToStringInvariant();
            return response["result"];
        }

        private async Task SendWebsocketAuth(IWebSocket socket) {
            var payload = await GetNoncePayloadAsync();
            var nonce = (payload["nonce"].ConvertInvariant<long>() + 5000).ToStringInvariant();
            var signature = CryptoUtility.SHA256Sign($"GET/realtime{nonce}", CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));
			await socket.SendMessageAsync(new { op = "auth", args = new [] {PublicApiKey.ToUnsecureString(), nonce, signature} });
        }

        private async Task<Dictionary<string, object>> GetAuthenticatedPayload(Dictionary<string, object> requestPayload = null)
        {
            var payload = await GetNoncePayloadAsync();
            var nonce = payload["nonce"].ConvertInvariant<long>();
            payload.Remove("nonce");
            payload["api_key"] = PublicApiKey.ToUnsecureString();
            payload["timestamp"] = nonce.ToStringInvariant();
            payload["recv_window"] = _recvWindow; 
            if (requestPayload != null)
            {
                payload = payload.Concat(requestPayload).ToDictionary(p => p.Key, p => p.Value);
            }

            string form = CryptoUtility.GetFormForPayload(payload, false, true);
            form = form.Replace("=False", "=false");
            form = form.Replace("=True", "=true");
            payload["sign"] = CryptoUtility.SHA256Sign(form, CryptoUtility.ToUnsecureBytesUTF8(PrivateApiKey));
            return payload;
        }

        private async Task<string> GetAuthenticatedQueryString(Dictionary<string, object> requestPayload = null)
        {
            var payload = await GetAuthenticatedPayload(requestPayload);
            var sign = payload["sign"].ToStringInvariant();
            payload.Remove("sign");
            string form = CryptoUtility.GetFormForPayload(payload, false, true);
            form += "&sign=" + sign;
            return form;
        }

        private Task<IWebSocket> DoConnectWebSocketAsync(Func<IWebSocket, Task> connected, Func<IWebSocket, JToken, Task> callback, int symbolArrayIndex = 3)
        {
			Timer pingTimer = null;
            return ConnectWebSocketAsync(url: string.Empty, messageCallback: async (_socket, msg) =>
            {
				var msgString = msg.ToStringFromUTF8();
                JToken token = JToken.Parse(msgString);

				if (token["ret_msg"]?.ToStringInvariant() == "pong")
				{ // received reply to our ping
					return;
				}

				if (token["topic"] != null)
                {
	                var data = token["data"];
		            await callback(_socket, data);
                } 
                else
                {
                    /*
                    subscription response:
                    {
                        "success": true, // Whether subscription is successful
                        "ret_msg": "",   // Successful subscription: "", otherwise it shows error message
                        "conn_id":"e0e10eee-4eff-4d21-881e-a0c55c25e2da",// current connection id
                        "request": {     // Request to your subscription
                            "op": "subscribe",
                            "args": [
                                "kline.BTCUSD.1m"
                            ]
                        }
                    }
                    */
                    JToken response = token["request"];
                    var op = response["op"]?.ToStringInvariant();
                    if ((response != null) && ((op == "subscribe") || (op == "auth"))) 
                    {
                        var responseMessage = token["ret_msg"]?.ToStringInvariant();
                        if (responseMessage != "")
                        {
						    Logger.Info("Websocket unable to connect: " + msgString);
					    	return;
					    }
                        else if (pingTimer == null)
                        {
                            /*
                            ping response:
                            {
                                "success": true, // Whether ping is successful
                                "ret_msg": "pong",
                                "conn_id": "036e5d21-804c-4447-a92d-b65a44d00700",// current connection id
                                "request": {
                                    "op": "ping",
                                    "args": null
                                }
                            }
                            */
                            pingTimer = new Timer(callback: async s => await _socket.SendMessageAsync(new { op = "ping" }),
                                state: null, dueTime: 0, period: 15000); // send a ping every 15 seconds
                            return;
                        }
                    }
				}
            }, 
            connectCallback: async (_socket) => 
            {
                await connected(_socket);
                _socket.ConnectInterval = TimeSpan.FromHours(0);
            }, 
            disconnectCallback: s =>
			{
				pingTimer.Dispose();
				pingTimer = null;
				return Task.CompletedTask;
			});
        }

        private async Task AddMarketSymbolsToChannel(IWebSocket socket, string argsPrefix, string[] marketSymbols)
        {
            string fullArgs = argsPrefix;
			if (marketSymbols == null || marketSymbols.Length == 0)
			{
				fullArgs += "*";
			} 
            else 
            {
                foreach (var symbol in marketSymbols)
                {
                    fullArgs += symbol + "|";
                } 
                fullArgs = fullArgs.TrimEnd('|');
            }

			await socket.SendMessageAsync(new { op = "subscribe", args = new [] {fullArgs} });
        }

        protected override async Task<IWebSocket> OnGetTradesWebSocketAsync(Func<KeyValuePair<string, ExchangeTrade>, Task> callback, params string[] marketSymbols)
        {
			/*
            request:
            {"op":"subscribe","args":["trade.BTCUSD|XRPUSD"]}
			*/
			/*
			    response:
                {
                    "topic": "trade.BTCUSD",
                    "data": [
                        {
                            "timestamp": "2020-01-12T16:59:59.000Z",
                            "trade_time_ms": 1582793344685, // trade time in millisecond
                            "symbol": "BTCUSD",
                            "side": "Sell",
                            "size": 328,
                            "price": 8098,
                            "tick_direction": "MinusTick",
                            "trade_id": "00c706e1-ba52-5bb0-98d0-bf694bdc69f7",
                            "cross_seq": 1052816407
                        }
                    ]
                }
			 */
			return await DoConnectWebSocketAsync(async (_socket) =>
			{
				await AddMarketSymbolsToChannel(_socket, "trade.", marketSymbols);
			}, async (_socket, token) =>
			{
                foreach (var dataRow in token)
                {
                    ExchangeTrade trade = dataRow.ParseTrade(
                        amountKey: "size", 
                        priceKey: "price",
                        typeKey: "side", 
                        timestampKey: "timestamp",
                        timestampType: TimestampType.Iso8601, 
                        idKey: "trade_id");
                    await callback(new KeyValuePair<string, ExchangeTrade>(dataRow["symbol"].ToStringInvariant(), trade));
                }
			});
        }

        public async Task<IWebSocket> GetPositionWebSocketAsync(Action<ExchangePosition> callback)
        {
			/*
            request:
            {"op": "subscribe", "args": ["position"]}
			*/
			/*
			    response:
                {
                "topic": "position",
                "action": "update",
                "data": [
                    {
                        "user_id":  1,                            // user ID
                        "symbol": "BTCUSD",                       // the contract for this position
                        "size": 11,                               // the current position amount
                        "side": "Sell",                           // side
                        "position_value": "0.00159252",           // positional value
                        "entry_price": "6907.291588174717",       // entry price
                        "liq_price": "7100.234",                  // liquidation price
                        "bust_price": "7088.1234",                // bankruptcy price
                        "leverage": "1",                           // leverage
                        "order_margin":  "1",                      // order margin
                        "position_margin":  "1",                   // position margin
                        "available_balance":  "2",                 // available balance
                        "take_profit": "0",                        // take profit price           
                        "tp_trigger_by":  "LastPrice",             // take profit trigger price, eg: LastPrice, IndexPrice. Conditional order only
                        "stop_loss": "0",                          // stop loss price
                        "sl_trigger_by":  "",                     // stop loss trigger price, eg: LastPrice, IndexPrice. Conditional order only
                        "realised_pnl":  "0.10",               // realised PNL
                        "trailing_stop": "0",                  // trailing stop points
                        "trailing_active": "0",                // trailing stop trigger price
                        "wallet_balance":  "4.12",             // wallet balance
                        "risk_id":  1,                       
                        "occ_closing_fee":  "0.1",             // position closing
                        "occ_funding_fee":  "0.1",             // funding fee
                        "auto_add_margin": 0,                  // auto margin replenishment switch
                        "cum_realised_pnl":  "0.12",           // Total realized profit and loss
                        "position_status": "Normal",           // status of position (Normal: normal Liq: in the process of liquidation Adl: in the process of Auto-Deleveraging)
                                        // Auto margin replenishment enabled (0: no 1: yes)
                        "position_seq": 14                     // position version number
                    }
                ]
                }
			 */
			return await DoConnectWebSocketAsync(async (_socket) =>
			{
                await SendWebsocketAuth(_socket);
			    await _socket.SendMessageAsync(new { op = "subscribe", args = new [] {"position"} });
			}, async (_socket, token) =>
			{
                foreach (var dataRow in token)
                {
                    callback(ParsePosition(dataRow));
                }
                await Task.CompletedTask;
			});
        }

        protected override async Task<IEnumerable<string>> OnGetMarketSymbolsAsync()
        {
            var m = await GetMarketSymbolsMetadataAsync();
            return m.Select(x => x.MarketSymbol);
        }

        protected internal override async Task<IEnumerable<ExchangeMarket>> OnGetMarketSymbolsMetadataAsync()
        {
            /*
            {
            "ret_code": 0,
            "ret_msg": "OK",
            "ext_code": "",
            "ext_info": "",
            "result": [
                {
                "name": "BTCUSD",
                "base_currency": "BTC",
                "quote_currency": "USD",
                "price_scale": 2,
                "taker_fee": "0.00075",
                "maker_fee": "-0.00025",
                "leverage_filter": {
                    "min_leverage": 1,
                    "max_leverage": 100,
                    "leverage_step": "0.01"
                },
                "price_filter": {
                    "min_price": "0.5",
                    "max_price": "999999.5",
                    "tick_size": "0.5"
                },
                "lot_size_filter": {
                    "max_trading_qty": 1000000,
                    "min_trading_qty": 1,
                    "qty_step": 1
                }
                },
                {
                "name": "ETHUSD",
                "base_currency": "ETH",
                "quote_currency": "USD",
                "price_scale": 2,
                "taker_fee": "0.00075",
                "maker_fee": "-0.00025",
                "leverage_filter": {
                    "min_leverage": 1,
                    "max_leverage": 50,
                    "leverage_step": "0.01"
                },
                "price_filter": {
                    "min_price": "0.05",
                    "max_price": "99999.95",
                    "tick_size": "0.05"
                },
                "lot_size_filter": {
                    "max_trading_qty": 1000000,
                    "min_trading_qty": 1,
                    "qty_step": 1
                }
                },
                {
                "name": "EOSUSD",
                "base_currency": "EOS",
                "quote_currency": "USD",
                "price_scale": 3,
                "taker_fee": "0.00075",
                "maker_fee": "-0.00025",
                "leverage_filter": {
                    "min_leverage": 1,
                    "max_leverage": 50,
                    "leverage_step": "0.01"
                },
                "price_filter": {
                    "min_price": "0.001",
                    "max_price": "1999.999",
                    "tick_size": "0.001"
                },
                "lot_size_filter": {
                    "max_trading_qty": 1000000,
                    "min_trading_qty": 1,
                    "qty_step": 1
                }
                },
                {
                "name": "XRPUSD",
                "base_currency": "XRP",
                "quote_currency": "USD",
                "price_scale": 4,
                "taker_fee": "0.00075",
                "maker_fee": "-0.00025",
                "leverage_filter": {
                    "min_leverage": 1,
                    "max_leverage": 50,
                    "leverage_step": "0.01"
                },
                "price_filter": {
                    "min_price": "0.0001",
                    "max_price": "199.9999",
                    "tick_size": "0.0001"
                },
                "lot_size_filter": {
                    "max_trading_qty": 1000000,
                    "min_trading_qty": 1,
                    "qty_step": 1
                }
                }
            ],
            "time_now": "1581411225.414179"
            }}
             */

            List<ExchangeMarket> markets = new List<ExchangeMarket>();
            JToken allSymbols = CheckRetCode(await DoMakeJsonRequestAsync<JToken>("/v2/public/symbols"));
			foreach (JToken marketSymbolToken in allSymbols)
            {
                var market = new ExchangeMarket
                {
                    MarketSymbol = marketSymbolToken["name"].ToStringUpperInvariant(),
                    IsActive = true,
                    QuoteCurrency = marketSymbolToken["quote_currency"].ToStringUpperInvariant(),
                    BaseCurrency = marketSymbolToken["base_currency"].ToStringUpperInvariant(),
                };

                try
                {
                    JToken priceFilter = marketSymbolToken["price_filter"];
                    market.MinPrice = priceFilter["min_price"].ConvertInvariant<decimal>();
                    market.MaxPrice = priceFilter["max_price"].ConvertInvariant<decimal>();
                    market.PriceStepSize = priceFilter["tick_size"].ConvertInvariant<decimal>();

                    JToken lotSizeFilter = marketSymbolToken["lot_size_filter"];
                    market.MinTradeSize = lotSizeFilter["min_trading_qty"].ConvertInvariant<decimal>();
                    market.MaxTradeSize = lotSizeFilter["max_trading_qty"].ConvertInvariant<decimal>();
                    market.QuantityStepSize = lotSizeFilter["qty_step"].ConvertInvariant<decimal>();
                }
                catch
                {

                }
                markets.Add(market);
            }
            return markets;
        }

        
        private async Task<Dictionary<string, decimal>> DoGetAmountsAsync(string field)
        {
            /*
            {
                "ret_code": 0,
                "ret_msg": "OK",
                "ext_code": "",
                "ext_info": "",
                "result": {
                    "BTC": {
                        "equity": 1002,                         //equity = wallet_balance + unrealised_pnl
                        "available_balance": 999.99987471,      //available_balance
                        //In Isolated Margin Mode:
                        // available_balance = wallet_balance - (position_margin + occ_closing_fee + occ_funding_fee + order_margin)
                        //In Cross Margin Mode:
                        //if unrealised_pnl > 0:
                        //available_balance = wallet_balance - (position_margin + occ_closing_fee + occ_funding_fee + order_margin)；
                        //if unrealised_pnl < 0:
                        //available_balance = wallet_balance - (position_margin + occ_closing_fee + occ_funding_fee + order_margin) + unrealised_pnl
                        "used_margin": 0.00012529,              //used_margin = wallet_balance - available_balance
                        "order_margin": 0.00012529,             //Used margin by order
                        "position_margin": 0,                   //position margin
                        "occ_closing_fee": 0,                   //position closing fee
                        "occ_funding_fee": 0,                   //funding fee
                        "wallet_balance": 1000,                 //wallet balance. When in Cross Margin mod, the number minus your unclosed loss is your real wallet balance.
                        "realised_pnl": 0,                      //daily realized profit and loss
                        "unrealised_pnl": 2,                    //unrealised profit and loss
                            //when side is sell:
                            // unrealised_pnl = size * (1.0 / mark_price -  1.0 / entry_price）
                            //when side is buy:
                            // unrealised_pnl = size * (1.0 / entry_price -  1.0 / mark_price）
                        "cum_realised_pnl": 0,                  //total relised profit and loss
                        "given_cash": 0,                        //given_cash
                        "service_cash": 0                       //service_cash
                    }
                },
                "time_now": "1578284274.816029",
                "rate_limit_status": 98,
                "rate_limit_reset_ms": 1580885703683,
                "rate_limit": 100
            }
            */
            Dictionary<string, decimal> amounts = new Dictionary<string, decimal>();
            var queryString = await GetAuthenticatedQueryString();
            JToken currencies = CheckRetCode(await DoMakeJsonRequestAsync<JToken>($"/v2/private/wallet/balance?" + queryString, BaseUrl, null, "GET"));
            foreach (JProperty currency in currencies.Children<JProperty>())
            {
                var balance = currency.Value[field].ConvertInvariant<decimal>();
                if (amounts.ContainsKey(currency.Name))
                {
                    amounts[currency.Name] += balance;
                }
                else
                {
                    amounts[currency.Name] = balance;
                }
            }
            return amounts;
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAsync()
        {
            return await DoGetAmountsAsync("equity");
        }

        protected override async Task<Dictionary<string, decimal>> OnGetAmountsAvailableToTradeAsync()
        {
            return await DoGetAmountsAsync("available_balance");
        }

        public async Task<IEnumerable<ExchangePosition>> GetCurrentPositionsAsync()
        {
            /*
            {
                "ret_code": 0,
                "ret_msg": "OK",
                "ext_code": "",
                "ext_info": "",
                "result": {
                    "id": 27913,
                    "user_id": 1,
                    "risk_id": 1,
                    "symbol": "BTCUSD",
                    "side": "Buy",
                    "size": 5,
                    "position_value": "0.0006947",
                    "entry_price": "7197.35137469",
                    "is_isolated":true,
                    "auto_add_margin": 0,
                    "leverage": "1",  //In Isolated Margin mode, the value is set by user. In Cross Margin mode, the value is the max leverage at current risk level
                    "effective_leverage": "1", // Effective Leverage. In Isolated Margin mode, its value equals `leverage`; In Cross Margin mode, The formula to calculate:
                        effective_leverage = position size / mark_price / (wallet_balance + unrealised_pnl)
                    "position_margin": "0.0006947",
                    "liq_price": "3608",
                    "bust_price": "3599",
                    "occ_closing_fee": "0.00000105",
                    "occ_funding_fee": "0",
                    "take_profit": "0",
                    "stop_loss": "0",
                    "trailing_stop": "0",
                    "position_status": "Normal",
                    "deleverage_indicator": 4,
                    "oc_calc_data": "{\"blq\":2,\"blv\":\"0.0002941\",\"slq\":0,\"bmp\":6800.408,\"smp\":0,\"fq\":-5,\"fc\":-0.00029477,\"bv2c\":1.00225,\"sv2c\":1.0007575}",
                    "order_margin": "0.00029477",
                    "wallet_balance": "0.03000227",
                    "realised_pnl": "-0.00000126",
                    "unrealised_pnl": 0,
                    "cum_realised_pnl": "-0.00001306",
                    "cross_seq": 444081383,
                    "position_seq": 287141589,
                    "created_at": "2019-10-19T17:04:55Z",
                    "updated_at": "2019-12-27T20:25:45.158767Z"
                },
                "time_now": "1577480599.097287",
                "rate_limit_status": 119,
                "rate_limit_reset_ms": 1580885703683,
                "rate_limit": 120
            }
            */
            var queryString = await GetAuthenticatedQueryString();
            JToken token = CheckRetCode(await DoMakeJsonRequestAsync<JToken>($"/v2/private/position/list?" + queryString, BaseUrl, null, "GET"));
            List<ExchangePosition> positions = new List<ExchangePosition>();
            foreach (var item in token)
            {
                positions.Add(ParsePosition(item["data"]));
            }
            return positions;
        }

        private async Task<IEnumerable<ExchangeOrderResult>> DoGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            var extraParams = new Dictionary<string, object>();

            if (orderId != null) 
            {
                extraParams["order_id"] = orderId;
            }

            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                extraParams["symbol"] = marketSymbol;
            }
            else
            {
                throw new Exception("marketSymbol is required");
            }
            
            var queryString = await GetAuthenticatedQueryString(extraParams);
            JToken token = GetResult(await DoMakeJsonRequestAsync<JToken>($"/v2/private/order?" + queryString, BaseUrl, null, "GET"), out var retCode, out var retMessage);

            List<ExchangeOrderResult> orders = new List<ExchangeOrderResult>();
            if (orderId == null) 
            {
                foreach (JToken order in token)
                {
                    orders.Add(ParseOrder(order, retCode, retMessage));
                }
            }
            else
            {
                orders.Add(ParseOrder(token, retCode, retMessage));
            }

            return orders;
        }

        //Note, Bybit is not recommending the use of "/v2/private/order/list" now that "/v2/private/order" is capable of returning multiple results
        protected override async Task<IEnumerable<ExchangeOrderResult>> OnGetOpenOrderDetailsAsync(string marketSymbol = null)
        {
            var orders = await DoGetOrderDetailsAsync(null, marketSymbol);
            return orders;
        }

        protected override async Task<ExchangeOrderResult> OnGetOrderDetailsAsync(string orderId, string marketSymbol = null)
        {
            var orders = await DoGetOrderDetailsAsync(orderId, marketSymbol);
            if (orders.Count() > 0)
            {
                return orders.First();
            }
            else
            {
                return null;
            }
        }

        protected override async Task OnCancelOrderAsync(string orderId, string marketSymbol = null)
        {
            var extraParams = new Dictionary<string, object>();
            extraParams["order_id"] = orderId;
            if (!string.IsNullOrWhiteSpace(marketSymbol))
            {
                extraParams["symbol"] = marketSymbol;
            }
            else
            {
                throw new Exception("marketSymbol is required");
            }
            
            var payload = await GetAuthenticatedPayload(extraParams);
            CheckRetCode(await DoMakeJsonRequestAsync<JToken>($"/v2/private/order/cancel", BaseUrl, payload, "POST"));
                // new string[] {"0", "30032"});
                //30032: order has been finished or canceled
        }
    
        public async Task CancelAllOrdersAsync(string marketSymbol)
        {
            var extraParams = new Dictionary<string, object>();
            extraParams["symbol"] = marketSymbol;
            var payload = await GetAuthenticatedPayload(extraParams);
            CheckRetCode(await DoMakeJsonRequestAsync<JToken>($"/v2/private/order/cancelAll", BaseUrl, payload, "POST"));
        }

        protected override async Task<ExchangeOrderResult> OnPlaceOrderAsync(ExchangeOrderRequest order)
        {
            var payload = new Dictionary<string, object>();
            await AddOrderToPayload(order, payload);
            payload = await GetAuthenticatedPayload(payload);
            JToken token = GetResult(await DoMakeJsonRequestAsync<JToken>("/v2/private/order/create", BaseUrl, payload, "POST"), out var retCode, out var retMessage);
            return ParseOrder(token, retCode, retMessage);
        }

        public async Task<ExchangeOrderResult> OnAmendOrderAsync(ExchangeOrderRequest order)
        {
            var payload = new Dictionary<string, object>();
            payload["symbol"] = order.MarketSymbol;
            if(order.OrderId != null)
                payload["order_id"] = order.OrderId;
            else if(order.ClientOrderId != null)
                payload["order_link_id"] = order.ClientOrderId;
            else 
                throw new Exception("Need either OrderId or ClientOrderId");

            payload["p_r_qty"] = (long) await ClampOrderQuantity(order.MarketSymbol, order.Amount);
            if(order.OrderType!=OrderType.Market)
                payload["p_r_price"] = order.Price;

            payload = await GetAuthenticatedPayload(payload);
            JToken token = GetResult(await DoMakeJsonRequestAsync<JToken>("/v2/private/order/replace", BaseUrl, payload, "POST"), out var retCode, out var retMessage);

            var result = new ExchangeOrderResult();
            result.ResultCode = retCode;
            result.Message = retMessage;
            if (retCode == "0")
                result.OrderId = token["order_id"].ToStringInvariant();
            return result;
        }

        private async Task AddOrderToPayload(ExchangeOrderRequest order, Dictionary<string, object> payload)
        {
            /*
            side	true	string	Side
            symbol	true	string	Symbol
            order_type	true	string	Active order type
            qty	true	integer	Order quantity in USD
            price	false	number	Order price
            time_in_force	true	string	Time in force
            take_profit	false	number	Take profit price, only take effect upon opening the position
            stop_loss	false	number	Stop loss price, only take effect upon opening the position
            reduce_only	false	bool	What is a reduce-only order? True means your position can only reduce in size if this order is triggered
            close_on_trigger	false	bool	What is a close on trigger order? For a closing order. It can only reduce your position, not increase it. If the account has insufficient available balance when the closing order is triggered, then other active orders of similar contracts will be cancelled or reduced. It can be used to ensure your stop loss reduces your position regardless of current available margin.
            order_link_id	false	string	Customised order ID, maximum length at 36 characters, and order ID under the same agency has to be unique.
            */

            payload["side"] = order.IsBuy ? "Buy" : "Sell";
            payload["symbol"] = order.MarketSymbol;
            payload["order_type"] = order.OrderType.ToStringInvariant();
            payload["qty"] = await ClampOrderQuantity(order.MarketSymbol, order.Amount);

            if(order.OrderType!=OrderType.Market)
                payload["price"] = order.Price;

            if(order.ClientOrderId != null)
                payload["order_link_id"] = order.ClientOrderId;

            if (order.ExtraParameters.TryGetValue("reduce_only", out var reduceOnly))
            {
                payload["reduce_only"] = reduceOnly;
            }

            if (order.ExtraParameters.TryGetValue("time_in_force", out var timeInForce))
            {
                payload["time_in_force"] = timeInForce;
            }
            else
            {
                payload["time_in_force"] = "GoodTillCancel";
            }
        }

        private ExchangePosition ParsePosition(JToken token)
        {
            /*
            "id": 27913,
            "user_id": 1,
            "risk_id": 1,
            "symbol": "BTCUSD",
            "side": "Buy",
            "size": 5,
            "position_value": "0.0006947",
            "entry_price": "7197.35137469",
            "is_isolated":true,
            "auto_add_margin": 0,
            "leverage": "1",  //In Isolated Margin mode, the value is set by user. In Cross Margin mode, the value is the max leverage at current risk level
            "effective_leverage": "1", // Effective Leverage. In Isolated Margin mode, its value equals `leverage`; In Cross Margin mode, The formula to calculate:
                effective_leverage = position size / mark_price / (wallet_balance + unrealised_pnl)
            "position_margin": "0.0006947",
            "liq_price": "3608",
            "bust_price": "3599",
            "occ_closing_fee": "0.00000105",
            "occ_funding_fee": "0",
            "take_profit": "0",
            "stop_loss": "0",
            "trailing_stop": "0",
            "position_status": "Normal",
            "deleverage_indicator": 4,
            "oc_calc_data": "{\"blq\":2,\"blv\":\"0.0002941\",\"slq\":0,\"bmp\":6800.408,\"smp\":0,\"fq\":-5,\"fc\":-0.00029477,\"bv2c\":1.00225,\"sv2c\":1.0007575}",
            "order_margin": "0.00029477",
            "wallet_balance": "0.03000227",
            "realised_pnl": "-0.00000126",
            "unrealised_pnl": 0,
            "cum_realised_pnl": "-0.00001306",
            "cross_seq": 444081383,
            "position_seq": 287141589,
            "created_at": "2019-10-19T17:04:55Z",
            "updated_at": "2019-12-27T20:25:45.158767Z
            */
            ExchangePosition result = new ExchangePosition
            {
                MarketSymbol = token["symbol"].ToStringUpperInvariant(),
                Amount = token["size"].ConvertInvariant<decimal>(),
                AveragePrice = token["entry_price"].ConvertInvariant<decimal>(),
                LiquidationPrice = token["liq_price"].ConvertInvariant<decimal>(),
                Leverage = token["effective_leverage"].ConvertInvariant<decimal>(),
                TimeStamp = CryptoUtility.ParseTimestamp(token["updated_at"], TimestampType.Iso8601)
            };
            if (token["side"].ToStringInvariant() == "Sell")
                result.Amount *= -1;
            return result;
        }

        private ExchangeOrderResult ParseOrder(JToken token, string resultCode, string resultMessage)
        {
            /*
            Active Order:
            {
            "ret_code": 0,
            "ret_msg": "OK",
            "ext_code": "",
            "ext_info": "",
            "result": {
                "user_id": 106958,
                "symbol": "BTCUSD",
                "side": "Buy",
                "order_type": "Limit",
                "price": "11756.5",
                "qty": 1,
                "time_in_force": "PostOnly",
                "order_status": "Filled",
                "ext_fields": {
                    "o_req_num": -68948112492,
                    "xreq_type": "x_create"
                },
                "last_exec_time": "1596304897.847944",
                "last_exec_price": "11756.5",
                "leaves_qty": 0,
                "leaves_value": "0",
                "cum_exec_qty": 1,
                "cum_exec_value": "0.00008505",
                "cum_exec_fee": "-0.00000002",
                "reject_reason": "",
                "cancel_type": "",
                "order_link_id": "",
                "created_at": "2020-08-01T18:00:26Z",
                "updated_at": "2020-08-01T18:01:37Z",
                "order_id": "e66b101a-ef3f-4647-83b5-28e0f38dcae0"
            },
            "time_now": "1597171013.867068",
            "rate_limit_status": 599,
            "rate_limit_reset_ms": 1597171013861,
            "rate_limit": 600
            }

            Active Order List:
            {
                "ret_code": 0,
                "ret_msg": "OK",
                "ext_code": "",
                "ext_info": "",
                "result": {
                    "data": [ 
                        {
                            "user_id": 160861,
                            "order_status": "Cancelled",
                            "symbol": "BTCUSD",
                            "side": "Buy",
                            "order_type": "Market",
                            "price": "9800",
                            "qty": "16737",
                            "time_in_force": "ImmediateOrCancel",
                            "order_link_id": "",
                            "order_id": "fead08d7-47c0-4d6a-b9e7-5c71d5df8ba1",
                            "created_at": "2020-07-24T08:22:30Z",
                            "updated_at": "2020-07-24T08:22:30Z",
                            "leaves_qty": "0",
                            "leaves_value": "0",
                            "cum_exec_qty": "0",
                            "cum_exec_value": "0",
                            "cum_exec_fee": "0",
                            "reject_reason": "EC_NoImmediateQtyToFill"
                        }
                    ],
                    "cursor": "w01XFyyZc8lhtCLl6NgAaYBRfsN9Qtpp1f2AUy3AS4+fFDzNSlVKa0od8DKCqgAn"
                },
                "time_now": "1604653633.173848",
                "rate_limit_status": 599,
                "rate_limit_reset_ms": 1604653633171,
                "rate_limit": 600
            }
            */
            ExchangeOrderResult result = new ExchangeOrderResult();
            if (token.Count() > 0)
            {
                result.Amount = token["qty"].ConvertInvariant<Decimal>();
                result.AmountFilled = token["cum_exec_qty"].ConvertInvariant<decimal>();
                result.Price = token["price"].ConvertInvariant<decimal>();
                result.IsBuy = token["side"].ToStringInvariant().EqualsWithOption("Buy");
                result.OrderDate = token["created_at"].ConvertInvariant<DateTime>();
                result.OrderId = token["order_id"].ToStringInvariant();
                result.ClientOrderId = token["order_link_id"].ToStringInvariant();
                result.MarketSymbol = token["symbol"].ToStringInvariant();

                switch (token["order_status"].ToStringInvariant())
                {
                    case "Created":
                    case "New":
                        result.Result = ExchangeAPIOrderResult.Pending;
                        break;
                    case "PartiallyFilled":
                        result.Result = ExchangeAPIOrderResult.FilledPartially;
                        break;
                    case "Filled":
                        result.Result = ExchangeAPIOrderResult.Filled;
                        break;
                    case "Cancelled":
                        result.Result = ExchangeAPIOrderResult.Canceled;
                        break;

                    default:
                        result.Result = ExchangeAPIOrderResult.Error;
                        break;
                }
            }
            result.ResultCode = resultCode;
            result.Message = resultMessage;

            return result;
        }
    }

    public partial class ExchangeName { public const string Bybit = "Bybit"; }
}
