namespace ExchangeSharp
{
	/// <summary>
	/// Extension helper methods.
	/// </summary>
	internal static class FTXExtensions
	{
		/// <summary>
		/// Cnvert FTX order status string to <see cref="ExchangeAPIOrderResult"/>.
		/// </summary>
		/// <param name="status">FTX order status string.</param>
		/// <returns><see cref="ExchangeAPIOrderResult"/></returns>
		internal static ExchangeAPIOrderResult ToExchangeAPIOrderResult(
				this string status,
				decimal remainingAmount
		)
		{
			return (status, remainingAmount) switch
			{
				("new", _) => ExchangeAPIOrderResult.PendingOpen,
				("open", _) => ExchangeAPIOrderResult.Open,
				("closed", 0) => ExchangeAPIOrderResult.Filled,
				("closed", _) => ExchangeAPIOrderResult.Canceled,
				_ => ExchangeAPIOrderResult.Unknown,
			};
		}
	}
}
