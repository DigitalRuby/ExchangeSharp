using System.Collections.Generic;
using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.Ndax.Models
{
    public class WithdrawTemplates : GenericResponse
    {
        [JsonProperty("TemplateTypes")]
        public IEnumerable<string> TemplateTypes { get; set; }
    }
}