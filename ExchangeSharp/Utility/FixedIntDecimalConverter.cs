namespace ExchangeSharp.Utility
{
	public class FixedIntDecimalConverter
	{
		private readonly decimal multiplier;

		public FixedIntDecimalConverter(int multiplier)
		{
			this.multiplier = decimal.Parse(
				1.ToString().PadRight(multiplier + 1, '0')
			);
		}

		public long FromDecimal(decimal value)
			=> (long) (value * multiplier);

		public decimal ToDecimal(long value)
			=> value / multiplier;
	}
}
