Pull requests are encouraged and welcome. The more people contributing, the more features and faster this API will grow, serving everyone who uses C# and cryptocurrency.

Please follow these guidelines...
- For JSON parsing and strings, please use the ```CryptoUtility``` extension methods ```ToStringInvariant``` and ```ConvertInvariant``` for all parsing or conversion to other values. This ensures that any locale machine will have no problems parsing and converting data. Do not use .Value<T> functions.
- Always, always search for existing code in the project before coding new functions. CryptoUtility has a lot of useful stuff.
- Keep the code simple and the project lean. We do NOT want to turn into https://github.com/knowm/XChange.
- When creating a new Exchange API, please do the following:
  - Override the protected methods of ExchangeAPI that you want to implement.
  - Ensure that the unit tests and integrations tests (ExchangeSharpConsole.exe test exchangeName=[name]) pass before submitting a pull request.
  - Easiest way is find another exchange and copy the file and customize as needed.
  - For any DateTime conversions, please use the ConvertDateTimeInvariant (BaseAPI.cs), and that you have set DateTimeAreLocal in constructor to true if needed.
  - Set additional protected and public properties in constructor as needed (SymbolSeparator, SymbolIsReversed, SymbolIsUppercase, etc.).
  - Please use CryptoUtility.UTF8EncodingNoPrefix for all encoding, unless you have specific needs.
  - For reference comparisons, https://github.com/ccxt/ccxt is a good project to compare against when creating a new exchange.
  - Before adding NuGet packages, consider copying in the bits of the source that matter into the Dependencies folder. The fewer packages that ExchangeSharp depends on, the better.
  - Each exchange API is in it's own folder. If you are creating model objects or helper classes for an exchange, put them inside a partial class or namespace for the exchange with internal instead of public classes and put in a new file in the exchange sub-folder. See Binance or Bittrex for an example.
 



