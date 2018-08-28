/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeZBcomAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "http://api.zb.com/data/v1";
        public override string BaseUrlWebSocket { get; set; } = "wss://api.zb.com:9999/websocket";

        public ExchangeZBcomAPI()
        {
            SymbolSeparator = "_";
            SymbolIsUppercase = false;
        }

        private string NormalizeSymbolWebsocket(string symbol)
        {
            if (symbol == null) return symbol;

            return (symbol ?? string.Empty).ToLowerInvariant().Replace("-", string.Empty);
        }

        #region publicAPI

        private async Task<Tuple<JToken, string>> MakeRequestZBcomAsync(string symbol, string subUrl, string baseUrl = null)
        {
            JToken obj = await MakeJsonRequestAsync<JToken>(subUrl.Replace("$SYMBOL$", symbol ?? string.Empty), baseUrl);
            return new Tuple<JToken, string>(obj, symbol);
        }

        private ExchangeTicker ParseTicker(string symbol, JToken data)
        {
            // {{"ticker":{"vol":"18202.5979","last":"6698.2","sell":"6703.21","buy":"6693.2","high":"6757.69","low":"6512.69"},"date":"1531822098779"}}
            return this.ParseTicker(data["ticker"], symbol, "sell", "buy", "last", "vol", "date", TimestampType.UnixMilliseconds);
        }

        private ExchangeTicker ParseTickerV2(string symbol, JToken data)
        {
            //{"hpybtc":{ "vol":"500450.0","last":"0.0000013949","sell":"0.0000013797","buy":"0.0000012977","high":"0.0000013949","low":"0.0000011892"}}
            return this.ParseTicker(data.First, symbol, "sell", "buy", "last", "vol");
        }

        protected override async Task<IEnumerable<string>> OnGetSymbolsAsync()
        {
            var data = await MakeRequestZBcomAsync(string.Empty, "/markets");
            List<string> symbols = new List<string>();
            foreach (JProperty prop in data.Item1)
            {
                symbols.Add(prop.Name);
            }
            return symbols;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            var data = await MakeRequestZBcomAsync(symbol, "/ticker?market=$SYMBOL$");
            return ParseTicker(data.Item2, data.Item1);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            //{ "hpybtc":{ "vol":"500450.0","last":"0.0000013949","sell":"0.0000013797","buy":"0.0000012977","high":"0.0000013949","low":"0.0000011892"},"tvqc":{ "vol":"2125511.1",

            var data = await MakeRequestZBcomAsync(null, "/allTicker", BaseUrl);
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            string symbol;
            foreach (JToken token in data.Item1)
            {
                symbol = token.Path;
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ParseTickerV2(symbol, token)));
            }
            return tickers;
        }

        protected override IWebSocket OnGetTradesWebSocket(Action<KeyValuePair<string, ExchangeTrade>> callback, params string[] symbols)
        {
            if (callback == null)
            {
                return null;
            }

            return ConnectWebSocket(string.Empty, (_socket, msg) =>
            {
                JToken token = JToken.Parse(msg.ToStringFromUTF8());
                if (token["dataType"].ToStringInvariant() == "trades")
                {
                    var channel = token["channel"].ToStringInvariant();
                    var sArray = channel.Split('_');
                    string symbol = sArray[0];
                    var data = token["data"];
                    var trades = ParseTradesWebsocket(data);
                    foreach (var trade in trades)
                    {
                        callback(new KeyValuePair<string, ExchangeTrade>(symbol, trade));
                    }
                }
                return Task.CompletedTask;

            }, async (_socket) =>
            {
                foreach (var symbol in symbols)
                {
                    string normalizedSymbol = NormalizeSymbolWebsocket(symbol);
                    await _socket.SendMessageAsync(new { @event = "addChannel", channel = normalizedSymbol + "_trades" });
                }
            });
        }

        private IEnumerable<ExchangeTrade> ParseTradesWebsocket(JToken token)
        {
            //{ "amount":"0.0372","price": "7509.7","tid": 153806522,"date": 1532103901,"type": "sell","trade_type": "ask"},{"amount": "0.0076", ...
            var trades = new List<ExchangeTrade>();
            foreach (var t in token)
            {
                trades.Add(t.ParseTrade("amount", "price", "type", "date", TimestampType.UnixSeconds, "tid"));
            }
            return trades;
        }

        #endregion

        #region Error processing

        private string StatusToError(string status)
        {
            switch (status)
            {
                case "1000": return "Success";
                case "1001": return "Error Tips";
                case "1002": return "Internal Error";
                case "1003": return "Validate No Pass";
                case "1004": return "Transaction Password Locked";
                case "1005": return "Transaction Password Error";
                case "1006": return "Real - name verification is pending approval or not approval";
                case "1009": return "This interface is in maintaining";
                case "1010": return "Not open yet";
                case "1012": return "Permission denied.";
                case "2001": return "Insufficient CNY Balance";
                case "2002": return "Insufficient BTC Balance";
                case "2003": return "Insufficient LTC Balance";
                case "2005": return "Insufficient ETH Balance";
                case "2006": return "Insufficient ETC Balance";
                case "2007": return "Insufficient BTS Balance";
                case "2009": return "Insufficient account balance";
                case "3001": return "Not Found Order";
                case "3002": return "Invalid Money";
                case "3003": return "Invalid Amount";
                case "3004": return "No Such User";
                case "3005": return "Invalid Parameters";
                case "3006": return "Invalid IP or Differ From the Bound IP";
                case "3007": return "Invalid Request Time";
                case "3008": return "Not Found Transaction Record";
                case "4001": return "API Interface is locked or not enabled";
                case "4002": return "Request Too Frequently";

                default: return status;
            }
        }

        #endregion
    }

    public partial class ExchangeName { public const string ZBcom = "ZBcom"; }
}
