using ExchangeSharp.Dependencies.Converters;
using Newtonsoft.Json;

namespace ExchangeSharp.BL3P
{
	internal class BL3PResponseConverter<TSuccess> : JsonComplexObjectConverter<BL3PResponsePayload>
		where TSuccess : BL3PResponsePayload, new()
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
					// this is the first prop on an error object
					case "code":
						return new BL3PResponsePayloadError();
					default:
						return new TSuccess();
				}
			}

			throw new JsonException("Could not locate key property in json.");
		}
	}
}
