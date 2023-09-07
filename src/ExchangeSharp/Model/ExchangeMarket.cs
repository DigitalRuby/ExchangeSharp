/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
	/// <summary>Representation of a market on an exchange.</summary>
	public class ExchangeMarket
	{
		/// <summary>Id of the market (specific to the exchange), null if none</summary>
		public string MarketId { get; set; }

		/// <summary>Gets or sets the symbol representing the market's currency pair.</summary>
		public string MarketSymbol { get; set; }

		/// <summary>Aternate market symbol</summary>
		public string AltMarketSymbol { get; set; }

		/// <summary>Second aternate market symbol</summary>
		public string AltMarketSymbol2 { get; set; }

		/// <summary>A value indicating whether the market is active.</summary>
		public bool? IsActive { get; set; }

		/// <summary>In a pair like ZRX/BTC, BTC is the quote currency.</summary>
		public string QuoteCurrency { get; set; }

		/// <summary>In a pair like ZRX/BTC, ZRX is the base currency.</summary>
		public string BaseCurrency { get; set; }

		/// <summary>The minimum size of the trade in the unit of "BaseCurrency". For example, in
		/// DOGE/BTC the MinTradeSize is currently 423.72881356 DOGE</summary>
		public decimal? MinTradeSize { get; set; }

		/// <summary>The maximum size of the trade in the unit of "BaseCurrency".</summary>
		public decimal? MaxTradeSize { get; set; }

		/// <summary>The minimum size of the trade in the unit of "QuoteCurrency". To determine an order's
		/// trade size in terms of the Quote Currency, you need to calculate: price * quantity
		/// NOTE: Not all exchanges provide this information</summary>
		public decimal? MinTradeSizeInQuoteCurrency { get; set; }

		/// <summary>The maximum size of the trade in the unit of "QuoteCurrency". To determine an order's
		/// trade size in terms of the Quote Currency, you need to calculate: price * quantity
		/// NOTE: Not all exchanges provide this information</summary>
		public decimal? MaxTradeSizeInQuoteCurrency { get; set; }

		/// <summary>The minimum price of the pair.</summary>
		public decimal? MinPrice { get; set; }

		/// <summary>The maximum price of the pair.</summary>
		public decimal? MaxPrice { get; set; }

		/// <summary>Defines the intervals that a price can be increased/decreased by. The following
		/// must be true for price: Price % PriceStepSize == 0 Null if unknown or not applicable.</summary>
		public decimal? PriceStepSize { get; set; }

		/// <summary>Defines the intervals that a quantity can be increased/decreased by. The
		/// following must be true for quantity: (Quantity-MinTradeSize) % QuantityStepSize == 0 Null
		/// if unknown or not applicable.</summary>
		public decimal? QuantityStepSize { get; set; }

		/// <summary>
		/// Margin trading enabled for this market
		/// </summary>
		public bool? MarginEnabled { get; set; }

		public override string ToString()
		{
			return $"{MarketSymbol}, {BaseCurrency}-{QuoteCurrency}";
		}
	}
}
