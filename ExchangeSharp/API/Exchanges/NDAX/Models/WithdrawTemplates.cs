using System.Collections.Generic;
using Newtonsoft.Json;

namespace ExchangeSharp.NDAX
{
    public class WithdrawTemplates : GenericResponse
    {
        [JsonProperty("TemplateTypes")]
        public IEnumerable<string> TemplateTypes { get; set; }
    }
}