/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

namespace ExchangeSharp
{
	using System;

	/// <summary>Result of an exchange order</summary>
	public sealed class ExchangeOrderResult
	{
		/// <summary>Order id</summary>
		public string OrderId { get; set; }

		/// <summary>
		/// Client Order id
		/// Order IDs put here in the Request will be returned by the exchange
		/// Not all exchanges support this
		/// </summary>
		public string ClientOrderId { get; set; }

		/// <summary>Result of the order</summary>
		public ExchangeAPIOrderResult Result { get; set; }

		/// <summary>
		/// Result/Error code from exchange
		/// Not all exchanges support this
		/// </summary>
		public string ResultCode { get; set; }

		/// <summary>Message if any</summary>
		public string Message { get; set; }

		/// <summary>
		/// Original order amount in the market currency.
		/// E.g. ADA/BTC would be ADA
		/// Consider making this property nullable in the future, such as for Coinbase
		/// </summary>
		public decimal Amount { get; set; }

		/// <summary>
		/// In the market currency. May be null if not provided by exchange
		/// If this is a Trade, then this is the amount filled in the trade. If it is an order, this is the cumulative amount filled in the order so far.
		/// </summary>
		public decimal? AmountFilled { get; set; }

		/// <summary>
		/// Some exchanges (such as coinbase) only provide RemainingSize.
		/// For these, AmountFilled will be set to remaining size/amount, and IsAmountFilledReversed will be set to true
		/// </summary>
		public bool IsAmountFilledReversed { get; set; }

		/// <summary>The limit price on the order in the ratio of base/market currency.
		/// E.g. 0.000342 ADA/ETH</summary>
		public decimal? Price { get; set; }

		/// <summary>Price per unit in the ratio of base/market currency. Note, that if this is a trade (TradeId is not null),
		/// this represents only the Avg Price on this particular trade/fill, not the Avg Price over the entire order.
		/// E.g. 0.000342 ADA/ETH</summary>
		public decimal? AveragePrice { get; set; }

		/// <summary>Order datetime in UTC</summary>
		public DateTime OrderDate { get; set; }

		/// <summary> raw HTTP header date </summary>
		public DateTime? HTTPHeaderDate { get; set; }

		/// <summary>datetime in UTC order was completed (could be filled, cancelled, expired, rejected, etc...). Null if still open.</summary>
		public DateTime? CompletedDate { get; set; }

		/// <summary>Market Symbol. E.g. ADA/ETH</summary>
		public string MarketSymbol { get; set; }

		/// <summary>Whether the order is a buy or sell</summary>
		public bool IsBuy { get; set; }

		/// <summary>The fees on the order (not a percent). Note, that if this is a trade (TradeId is not null),
		/// this represents only the fees on this particular trade/fill, not the cumulative amount in the order.
		/// E.g. 0.0025 ETH</summary>
		public decimal? Fees { get; set; }

		/// <summary>The currency the fees are in.
		/// If not set, this is probably the base currency</summary>
		public string FeesCurrency { get; set; }

		/// <summary>The id of the trade if this is only one trade out of the order.</summary>
		public string TradeId { get; set; }

		/// <summary>datetime in UTC of the trade. Null if not a trade.</summary>
		public DateTime? TradeDate { get; set; }

		/// <summary>
		/// sequence that the order update was sent from the server, usually used to keep updates in order or for debugging purposes.
		/// Not all exchanges provide this value, so it may be null.
		/// </summary>
		public long? UpdateSequence { get; set; }

		/// <summary>Append another order to this order - order id and type must match</summary>
		/// <param name="other">Order to append</param>
		public void AppendOrderWithOrder(ExchangeOrderResult other)
		{
			if (
					(OrderId != null)
					&& (MarketSymbol != null)
					&& (
							(OrderId != other.OrderId)
							|| (IsBuy != other.IsBuy)
							|| (MarketSymbol != other.MarketSymbol)
					)
			)
			{
				throw new InvalidOperationException(
						"Appending orders requires order id, market symbol and is buy to match"
				);
			}

			decimal tradeSum = Amount + other.Amount;
			decimal baseAmount = Amount;
			Amount += other.Amount;
			AmountFilled += other.AmountFilled;
			Fees += other.Fees;
			FeesCurrency = other.FeesCurrency;
			AveragePrice =
					(AveragePrice * (baseAmount / tradeSum))
					+ (other.AveragePrice * (other.Amount / tradeSum));
			OrderId = other.OrderId;
			OrderDate = OrderDate == default ? other.OrderDate : OrderDate;
			MarketSymbol = other.MarketSymbol;
			IsBuy = other.IsBuy;
		}

		/// <summary>Returns a string that represents this instance.</summary>
		/// <returns>A string that represents this instance.</returns>
		public override string ToString()
		{
			return $"[{OrderDate}], {(IsBuy ? "Buy" : "Sell")} {AmountFilled} of {Amount} {MarketSymbol} {Result} at {AveragePrice}, fees paid {Fees} {FeesCurrency}";
		}
	}
}
