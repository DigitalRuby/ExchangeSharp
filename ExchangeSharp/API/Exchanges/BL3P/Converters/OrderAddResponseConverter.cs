using ExchangeSharp.API.Exchanges.BL3P.Models;
using ExchangeSharp.API.Exchanges.BL3P.Models.Orders.Add;
using ExchangeSharp.Dependencies.Converters;
using Newtonsoft.Json;

namespace ExchangeSharp.API.Exchanges.BL3P.Converters
{
	internal class OrderAddResponseConverter : JsonComplexObjectConverter<BL3PResponsePayload>
	{
		protected override BL3PResponsePayload Create(JsonReader reader)
		{
			while (reader.Read())
			{
				if (reader.TokenType != JsonToken.PropertyName)
				{
					continue;
				}

				var prop = (string) reader.Value;

				switch (prop)
				{
					case "order_id":
						return new BL3POrderAddSuccess();
					case "code":
						return new BL3PResponsePayloadError();
				}
			}

			throw new JsonException("Could not locate key property in json.");
		}
	}
}
