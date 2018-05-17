namespace ExchangeSharp
{
    public class ExchangeSequencedWebsocketMessage<T>
    {
        public ExchangeSequencedWebsocketMessage(int sequenceNumber, T data)
        {
            SequenceNumber = sequenceNumber;
            Data = data;
        }

        #region Properties

        public int SequenceNumber { get; }

        public T Data { get; }

        #endregion
    }
}