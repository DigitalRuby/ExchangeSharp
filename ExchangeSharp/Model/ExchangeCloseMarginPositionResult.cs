using System;

namespace ExchangeSharp
{
    public class ExchangeCloseMarginPositionResult
    {
        #region Properties

        public bool Success { get; set; }
        public string Message { get; set; }
        public bool IsBuy { get; set; }
        public string Symbol { get; set; }
        public string FeesCurrency { get; set; }
        public decimal AmountFilled { get; set; }
        public decimal AveragePrice { get; set; }
        public DateTime CloseDate { get; set; }
        public decimal Fees { get; set; }
        public string[] TradeIds { get; set; }

        #endregion
    }
}