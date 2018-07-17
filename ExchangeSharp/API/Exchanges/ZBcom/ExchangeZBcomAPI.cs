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
    public sealed class ExchangeZBcomAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "http://api.zb.com/data/v1";
        public override string Name => ExchangeName.ZBcom;

        public ExchangeZBcomAPI()
        {
            SymbolSeparator = "_";
        }

        public override string NormalizeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).ToLowerInvariant().Replace('-', '_');
        }

        public override string ExchangeSymbolToGlobalSymbol(string symbol)
        {
            return ExchangeSymbolToGlobalSymbolWithSeparator(symbol, SymbolSeparator[0]);
        }

        public override string GlobalSymbolToExchangeSymbol(string symbol)
        {
            return (symbol ?? string.Empty).ToLowerInvariant().Replace('-', '_');
        }

        #region publicAPI

        private async Task<Tuple<JToken, string>> MakeRequestZBcomAsync(string symbol, string subUrl, string baseUrl = null)
        {
            symbol = NormalizeSymbol(symbol);
            JToken obj = await MakeJsonRequestAsync<JToken>(subUrl.Replace("$SYMBOL$", symbol ?? string.Empty), baseUrl);
            return new Tuple<JToken, string>(obj, symbol);
        }

        private ExchangeTicker ParseTicker(string symbol, JToken data, DateTime? date)
        {
            // {{"ticker":{"vol":"18202.5979","last":"6698.2","sell":"6703.21","buy":"6693.2","high":"6757.69","low":"6512.69"},"date":"1531822098779"}}

            JToken ticker = data["ticker"];
            decimal last = ticker["last"].ConvertInvariant<decimal>();
            decimal vol = ticker["vol"].ConvertInvariant<decimal>();
            return new ExchangeTicker
            {
                Ask = ticker["sell"].ConvertInvariant<decimal>(),
                Bid = ticker["buy"].ConvertInvariant<decimal>(),
                Last = last,
                Volume = new ExchangeVolume
                {
                    BaseVolume = vol,
                    BaseSymbol = symbol,
                    ConvertedVolume = vol * last,
                    ConvertedSymbol = symbol,
                    Timestamp = date ?? CryptoUtility.UnixTimeStampToDateTimeMilliseconds(data["date"].ConvertInvariant<long>())
                }
            };
        }

        private ExchangeTicker ParseTickerV2(string symbol, JToken data, DateTime date)
        {
            //{"hpybtc":{ "vol":"500450.0","last":"0.0000013949","sell":"0.0000013797","buy":"0.0000012977","high":"0.0000013949","low":"0.0000011892"}}

            JToken ticker = data.First;
            decimal last = ticker["last"].ConvertInvariant<decimal>();
            decimal vol = ticker["vol"].ConvertInvariant<decimal>();
            return new ExchangeTicker
            {
                Ask = ticker["sell"].ConvertInvariant<decimal>(),
                Bid = ticker["buy"].ConvertInvariant<decimal>(),
                Last = last,
                Volume = new ExchangeVolume
                {
                    BaseVolume = vol,
                    BaseSymbol = symbol,
                    ConvertedVolume = vol * last,
                    ConvertedSymbol = symbol,
                    Timestamp = date
                }
            };
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string symbol)
        {
            var data = await MakeRequestZBcomAsync(symbol, "/ticker?market=$SYMBOL$");
            return ParseTicker(data.Item2, data.Item1, null);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            //{ "hpybtc":{ "vol":"500450.0","last":"0.0000013949","sell":"0.0000013797","buy":"0.0000012977","high":"0.0000013949","low":"0.0000011892"},"tvqc":{ "vol":"2125511.1",

            var data = await MakeRequestZBcomAsync(null, "/allTicker", BaseUrl);
            var date = DateTime.Now; //ZB.com doesn't give a timestamp when asking all tickers
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();
            string symbol;
            foreach (JToken token in data.Item1)
            {
                symbol = token.Path;
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(symbol, ParseTickerV2(symbol, token, date)));
            }
            return tickers;
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
}
