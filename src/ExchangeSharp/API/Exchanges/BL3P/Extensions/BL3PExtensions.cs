namespace ExchangeSharp.BL3P
{
	internal static class BL3PExtensions
	{
		public static ExchangeAPIOrderResult ToResult(this BL3POrderStatus status, BL3PAmount amount)
		{
			return status switch
			{
				BL3POrderStatus.Cancelled => ExchangeAPIOrderResult.Canceled,
				BL3POrderStatus.Closed => ExchangeAPIOrderResult.Filled,
				BL3POrderStatus.Open when amount.Value > 0 => ExchangeAPIOrderResult.FilledPartially,
				BL3POrderStatus.Open => ExchangeAPIOrderResult.Pending,
				BL3POrderStatus.Pending => ExchangeAPIOrderResult.Pending,
				BL3POrderStatus.Placed => ExchangeAPIOrderResult.Pending,
				_ => ExchangeAPIOrderResult.Unknown
			};
		}
	}
}
