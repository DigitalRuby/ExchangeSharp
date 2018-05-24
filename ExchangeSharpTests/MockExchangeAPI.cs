namespace ExchangeSharpTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using ExchangeSharp;

    public class MockExchangeAPI : ExchangeAPI
    {
        public int OnGetSymbolsMetadataAsyncCalls;

        public override string BaseUrl { get; set; }

        public override string Name { get; }

        public new ExchangeMarket GetExchangeMarket(string symbol)
        {
            return base.GetExchangeMarket(symbol);
        }

        public void SetExchangeMarkets(IEnumerable<ExchangeMarket> markets)
        {
            this.exchangeMarkets = markets;
        }

        protected override async Task<IEnumerable<ExchangeMarket>> OnGetSymbolsMetadataAsync()
        {
            this.OnGetSymbolsMetadataAsyncCalls++;
            return await Task.Run(() => new List<ExchangeMarket>());
        }
    }
}