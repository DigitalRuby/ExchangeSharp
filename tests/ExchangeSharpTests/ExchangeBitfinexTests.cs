/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Net;
using System.Security;
using ExchangeSharp;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExchangeSharpTests
{
	[TestClass]
	public class ExchangeBitfinexTests
	{
		private SecureString _pvtKey = new NetworkCredential("", "privKey").SecurePassword;
		private SecureString _pubKey = new NetworkCredential("", "SecKey").SecurePassword;

		[TestMethod]
		public void SubmitStopMarginOrder()
		{
			IExchangeAPI api = ExchangeAPI.GetExchangeAPIAsync("Bitfinex").Result;
			ExchangeOrderRequest order = new ExchangeOrderRequest
			{
				MarketSymbol = "ADAUSD",
				Amount = System.Convert.ToDecimal(0.0001),
				IsBuy = true,
				IsMargin = true,
				OrderType = OrderType.Stop,
				StopPrice = System.Convert.ToDecimal(100)
			};
			api.PrivateApiKey = _pvtKey;
			api.PublicApiKey = _pubKey;
			ExchangeOrderResult result = api.PlaceOrderAsync(order).Result;
		}

		[TestMethod]
		public void GetDataFromMarketWithSpecialChar()
		{
			IExchangeAPI api = ExchangeAPI.GetExchangeAPIAsync("Bitfinex").Result;
			string marketTicker = "DOGE:USD";
			DateTime start = new DateTime(2021, 12, 1);
			DateTime end = DateTime.Today;
			System.Collections.Generic.IEnumerable<MarketCandle> result = api.GetCandlesAsync(
					marketTicker,
					86400,
					start,
					end,
					1000
			).Result;
			result.Should().HaveCountGreaterThan(0, "Returned data");
		}
	}
}
