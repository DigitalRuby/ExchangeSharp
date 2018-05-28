namespace ExchangeSharp
{
    public class ExchangeMarginPositionResult
    {
        public string Symbol { get; set; }

        public decimal Amount { get; set; }

        public decimal Total { get; set; }

        public decimal ProfitLoss { get; set; }

        public decimal LendingFees { get; set; }

        public string Type { get; set; }

        public decimal BasePrice { get; set; }

        public decimal LiquidationPrice { get; set; }
    }
}
