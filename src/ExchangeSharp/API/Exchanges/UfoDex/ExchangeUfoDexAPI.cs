/*
MIT LICENSE
Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ExchangeSharp
{
    public sealed partial class ExchangeUfoDexAPI : ExchangeAPI
    {
        // TODO: Set correct base url
        public override string BaseUrl { get; set; } = "https://ufodex.io/dexsrv/mainnet/api/v1";

        private ExchangeTicker ParseTicker(JToken token)
        {
            string pair = token["Label"].ToStringInvariant();
            string[] symbols = pair.Split('/');
            return new ExchangeTicker
            {
                // TODO: Parse out fields...
                // Ticker JSON { "GenTime":12345678901234 "Label":"UFO/BTC", "Ask":0.00000005, "Bid":0.00000003, "Open":0.00000006, "High":0.00000007, "Low":0.00000004, "Close":0.00000003, "Volume":3240956.04453450, "BaseVolume":455533325.98457433 }
                Id = token["GenTime"].ConvertInvariant<string>(), // ????
                Ask = token["Ask"].ConvertInvariant<decimal>(),
                Bid = token["Bid"].ConvertInvariant<decimal>(),
                Last = token["Close"].ConvertInvariant<decimal>(),
                MarketSymbol = pair,
                Volume = new ExchangeVolume 
                {
                    QuoteCurrency       = symbols[0],
                    BaseCurrency        = symbols[1],
                    QuoteCurrencyVolume = token["Volume"].ConvertInvariant<decimal>(),
                    BaseCurrencyVolume  = token["BaseVolume"].ConvertInvariant<decimal>(),
                    Timestamp           = token["GenTime"].ConvertInvariant<long>().UnixTimeStampToDateTimeMilliseconds()
                }
            };
        }

        public ExchangeUfoDexAPI()
        {
            RequestContentType = "application/json";

            // TODO: Verify these are correct
            NonceStyle = NonceStyle.UnixMillisecondsString;
            MarketSymbolSeparator = "/";
            MarketSymbolIsUppercase = true;
        }

        protected override async Task<ExchangeTicker> OnGetTickerAsync(string marketSymbol)
        {
            // marketSymbol like "UFO/BTC"
            JToken result = await MakeJsonRequestAsync<JToken>("/getticker/" + marketSymbol);
            return ParseTicker(result);
        }

        protected override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> OnGetTickersAsync()
        {
            List<KeyValuePair<string, ExchangeTicker>> tickers = new List<KeyValuePair<string, ExchangeTicker>>();

            JToken result = await MakeJsonRequestAsync<JToken>("/gettickers");
            foreach (JProperty token in result)
            {
                // {"UFO/BTC":{Ticker JSON}, "UFO/LTC":{Ticker JSON}, ...}
                tickers.Add(new KeyValuePair<string, ExchangeTicker>(token.Name, ParseTicker(token.Value)));
            }
            return tickers;
        }
    }

    public partial class ExchangeName { public const string UfoDex = "UfoDex"; }
}