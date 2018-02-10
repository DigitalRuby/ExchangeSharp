namespace ExchangeSharp.Model
{
    public class ExchangeWithdrawalRequest
    {
        /// <summary>
        /// Gets or sets the asset.
        /// </summary>
        /// <value>
        /// The asset.
        /// </value>
        public string Asset { get; set; }
        /// <summary>
        /// Gets or sets to address.
        /// </summary>
        /// <value>
        /// To address.
        /// </value>
        public string ToAddress { get; set; }
        /// <summary>
        /// Gets or sets the address tag.
        /// </summary>
        /// <value>
        /// The address tag.
        /// </value>
        public string AddressTag { get; set; }
        /// <summary>
        /// Gets or sets the Amount.
        /// </summary>
        /// <value>
        /// The Amount.
        /// </value>
        public decimal Amount { get; set; }
        /// <summary>
        /// Description of the address
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        public string Name { get; set; }
    }
}