namespace ExchangeSharp
{
    using System;

    public class ExchangeCoinTransfer
    {
        public string Address { get; set; }

        public decimal Amounts { get; set; }

        public bool Authorized { get; set; }

        public bool Cancelled { get; set; }

        public string Currency { get; set; }

        public bool InvalidAddress { get; set; }

        public DateTime Opened { get; set; }

        public string PaymentId { get; set; }

        public bool PendingPayment { get; set; }

        public decimal TxCost { get; set; }

        public string TxId { get; set; }
    }
}