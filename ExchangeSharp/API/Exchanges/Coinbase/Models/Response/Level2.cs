namespace ExchangeSharp.Coinbase
{
    using System;
    using System.Collections.Generic;

    using Newtonsoft.Json;

    public class Level2 : BaseMessage
    {
        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        public DateTime Time { get; set; }

        public List<string[]> Changes { get; set; }
    }
}