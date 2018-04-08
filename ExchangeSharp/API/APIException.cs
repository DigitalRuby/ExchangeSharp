namespace ExchangeSharp
{
    using System;

    /// <summary>
    /// Exception class for API calls
    /// </summary>
    public class APIException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">Message</param>
        public APIException(string message) : base(message) { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"></param>
        /// <param name="innerException">Inner exception</param>
        public APIException(string message, Exception innerException) : base(message, innerException) { }
    }
}