namespace ExchangeSharp
{
    public class ExchangeSequencedWebsocketMessage<T>
    {
        public ExchangeSequencedWebsocketMessage(long sequenceNumber, T data)
        {
            SequenceNumber = sequenceNumber;
            Data = data;
        }

        #region Properties

        public long SequenceNumber { get; }

        public T Data { get; }

        #endregion
    }
}