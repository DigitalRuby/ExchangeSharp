using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace ExchangeSharp
{
    public abstract class BaseConverter<T> : JsonConverter
    {
        protected abstract Dictionary<T, string> Mapping { get; }
        private readonly bool quotes;

        protected BaseConverter(bool useQuotes)
        {
            quotes = useQuotes;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (quotes)
                writer.WriteValue(Mapping[(T)value]);
            else
                writer.WriteRawValue(Mapping[(T)value]);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value == null)
                return null;

            var value = reader.Value.ToString();
            if (Mapping.ContainsValue(value))
                return Mapping.Single(m => m.Value == value).Key;

            var lowerResult = Mapping.SingleOrDefault(m => m.Value.ToLower() == value.ToLower());
            if (!lowerResult.Equals(default(KeyValuePair<T, string>)))
                return lowerResult.Key;

            // Debug.WriteLine($"Cannot map enum. Type: {typeof(T)}, Value: {value}");
            return null;
        }

        public T ReadString(string data)
        {
            return Mapping.Single(v => v.Value == data).Key;
        }

        public override bool CanConvert(Type objectType)
        {
            // Check if it is type, or nullable of type
            return objectType == typeof(T) || Nullable.GetUnderlyingType(objectType) == typeof(T);
        }
    }
}
