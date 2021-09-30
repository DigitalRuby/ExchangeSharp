namespace ExchangeSharp
{
	/// <summary>
	/// Extension helper methods.
	/// </summary>
	internal static class Extensions
	{
		/// <summary>
		/// Cnvert FTX order status string to <see cref="ExchangeAPIOrderResult"/>.
		/// </summary>
		/// <param name="status">FTX order status string.</param>
		/// <returns><see cref="ExchangeAPIOrderResult"/></returns>
		internal static ExchangeAPIOrderResult ToExchangeAPIOrderResult(this string status)
		{
			return status switch
			{
				"open" => ExchangeAPIOrderResult.Pending,
				"closed" => ExchangeAPIOrderResult.Filled,
				_ => ExchangeAPIOrderResult.Unknown,
			};
		}
	}
}
