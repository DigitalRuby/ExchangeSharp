namespace ExchangeSharp
{
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class Snapshot : BaseMessage
    {
        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        public List<decimal[]> Bids { get; set; }

        public List<decimal[]> Asks { get; set; }
    }
}