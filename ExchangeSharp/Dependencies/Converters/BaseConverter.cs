/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

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

            var lowerResult = Mapping.SingleOrDefault(m => m.Value.ToLowerInvariant() == value.ToLowerInvariant());
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
