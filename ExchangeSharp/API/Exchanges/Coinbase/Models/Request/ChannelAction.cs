namespace ExchangeSharp
{
    using System.Collections.Generic;

    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    public class ChannelAction
    {
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty("type")]
        public ActionType Type { get; set; }

        [JsonProperty("channels")]
        public List<Channel> Channels { get; set; }
    }
}