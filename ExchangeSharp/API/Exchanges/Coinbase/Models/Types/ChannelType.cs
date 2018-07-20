namespace ExchangeSharp
{
    using System.Runtime.Serialization;

    public enum ChannelType
    {
        [EnumMember(Value = "full")]
        Full,

        [EnumMember(Value = "heartbeat")]
        Heartbeat,

        [EnumMember(Value = "level2")]
        Level2,

        [EnumMember(Value = "matches")]
        Matches,

        [EnumMember(Value = "ticker")]
        Ticker,

        [EnumMember(Value = "user")]
        User
    }
}