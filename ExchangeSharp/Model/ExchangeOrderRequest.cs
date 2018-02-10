namespace ExchangeSharp
{
    /// <summary>
    /// Order request details
    /// </summary>
    [System.Serializable]
    public class ExchangeOrderRequest
    {
        /// <summary>
        /// Symbol or pair for the order, i.e. btcusd
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// Amount to buy or sell
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// The price to buy or sell at
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// True if this is a buy, false if a sell
        /// </summary>
        public bool IsBuy { get; set; }

        /// <summary>
        /// Whether the Amount should be rounded - set to false if you know the exact Amount, otherwise leave
        /// as true so that the exchange does not reject the order due to too many decimal places.
        /// </summary>
        public bool ShouldRoundAmount { get; set; } = true;

        /// <summary>
        /// The type of order - only limit is supported for now
        /// </summary>
        public OrderType OrderType { get; set; } = OrderType.Limit;

        /// <summary>
        /// Return a rounded Amount if needed
        /// </summary>
        /// <returns>Rounded Amount or Amount if no rounding is needed</returns>
        public decimal RoundAmount()
        {
            return (ShouldRoundAmount ? CryptoUtility.RoundAmount(Amount) : Amount);
        }
    }
}