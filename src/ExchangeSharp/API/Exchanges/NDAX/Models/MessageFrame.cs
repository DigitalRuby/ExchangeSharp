using Newtonsoft.Json;

namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		class MessageFrame
		{
			/// <summary>
			/// The type of the message.
			/// </summary>
			[JsonProperty("m")]
			public MessageType MessageType { get; set; }

			/// <summary>
			/// The sequence number identifies an individual request or request-and-response pair.
			/// </summary>
			/// <remarks>
			/// A non-zero sequence number is required, but the numbering scheme you use is up to you.
			/// </remarks>
			[JsonProperty("i")]
			public long SequenceNumber { get; set; }

			/// <summary>
			/// The function name is the name of the function being called or that the server responds to.
			/// </summary>
			/// <remarks>
			/// The server response echoes the request.
			/// </remarks>
			[JsonProperty("n")]
			public string FunctionName { get; set; }

			/// <summary>
			/// Payload is a JSON-formatted string containing the data being sent with the message.
			/// </summary>
			[JsonProperty("o")]
			public string Payload { get; set; }

			/// <summary>
			/// Deserialized the <c>Payload</c> to an object of arbitrary type <typeparamref name="T"/>.
			/// </summary>
			/// <typeparam name="T">The type of the payload.</typeparam>
			/// <returns>The  <c>Payload</c> deserialized to the specified type.</returns>
			public T PayloadAs<T>()
			{
				return JsonConvert.DeserializeObject<T>(Payload);
			}
		}
	}
}
