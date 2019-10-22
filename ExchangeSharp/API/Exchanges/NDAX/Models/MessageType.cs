namespace ExchangeSharp
{
	public sealed partial class ExchangeNDAXAPI
	{
		enum MessageType
		{
			/// <summary>
			/// Request.
			/// </summary>
			Request = 0,

			/// <summary>
			/// Reply.
			/// </summary>
			Reply = 1,

			/// <summary>
			/// Subscribe to event.
			/// </summary>
			SubscribeToEvent = 2,

			/// <summary>
			/// Event.
			/// </summary>
			Event = 3,

			/// <summary>
			/// Unsubscribe from event.
			/// </summary>
			UnsubscribeFromEvent = 4,

			/// <summary>
			/// Error.
			/// </summary>
			Error = 5
		}
	}
}
