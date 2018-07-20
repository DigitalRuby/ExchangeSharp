namespace ExchangeSharp
{
    using System.Collections.Generic;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class Channel
    {
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("name")]
        public ChannelType Name { get; set; }

        [JsonProperty("product_ids")]
        public List<string> ProductIds { get; set; }
    }
}