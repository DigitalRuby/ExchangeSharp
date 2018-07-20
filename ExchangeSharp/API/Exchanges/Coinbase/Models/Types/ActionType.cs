namespace ExchangeSharp
{
    using System.Runtime.Serialization;

    public enum ActionType
    {
        [EnumMember(Value = "subscribe")]
        Subscribe,

        [EnumMember(Value = "unsubscribe")]
        Unsubscribe
    }
}