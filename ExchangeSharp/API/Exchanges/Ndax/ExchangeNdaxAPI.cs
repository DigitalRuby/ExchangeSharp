using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExchangeSharp.API.Exchanges.Ndax.Models;

namespace ExchangeSharp
{
    public sealed partial class ExchangeNdaxAPI : ExchangeAPI
    {
        public override string BaseUrl { get; set; } = "https://ndax.io/api";
        public override string Name => ExchangeName.Ndax;

        private static Dictionary<string, long> _symbolToIdMapping;

        public ExchangeNdaxAPI()
        {
            MarketSymbolSeparator = "_";
        }
        public override async Task<IEnumerable<KeyValuePair<string, ExchangeTicker>>> GetTickersAsync()
        {
            var result = await MakeJsonRequestAsync<Dictionary<string, NdaxTicker>>("returnticker");
            _symbolToIdMapping = result.ToDictionary(pair => pair.Key, pair => pair.Value.Id);
            return result.Select(pair =>
                new KeyValuePair<string, ExchangeTicker>(pair.Key, pair.Value.ToExchangeTicker(pair.Key)));
        }

        public override async Task<ExchangeTicker> GetTickerAsync(string marketSymbol)
        {
            if (_symbolToIdMapping == null)
            {
                await GetTickersAsync();
            }
            var result = await MakeJsonRequestAsync<Dictionary<string, NdaxTicker>>("returnticker",null, new Dictionary<string, object>()
            {
                {"InstrumentId", _symbolToIdMapping[marketSymbol]}
            });
            return result[marketSymbol].ToExchangeTicker(marketSymbol);
        }
    }


    public partial class ExchangeName
    {
        public const string Ndax = "Ndax";
    }
}