Pull requests are encouraged and welcome. The more people contributing, the more features and faster this API will grow, serving everyone who uses C# and cryptocurrency.

Please follow these guidelines...
- For JSON parsing and strings, please use the ```CryptoUtility``` extension methods ```ToStringInvariant``` and ```ConvertInvariant``` for all parsing or conversion to other values. This ensures that any locale machine will have no problems parsing and converting data.
- Always, always search for existing code in the project before coding new functions. CryptoUtility has a lot of useful stuff.
- Keep the code simple and the project lean. We do NOT want to turn into https://github.com/knowm/XChange.
- When creating a new Exchange API, please do the following:
  - Override the protected methods of ExchangeAPI that you want to implement.
  - Ensure that the unit tests and integrations tests (ExchangeSharpConsole.exe test exchangeName=[name]) pass before submitting a pull request.
  - Easiest way is find another exchange and copy the file and customize as needed.
  - For any DateTime conversions, please use the ConvertDateTimeInvariant (BaseAPI.cs), and that you have set DateTimeAreLocal in constructor to true if needed.
  - Set additional protected and public properties in constructor as needed (SymbolSeparator, SymbolIsReversed, SymbolIsUppercase, etc.).
  - Please use CryptoUtility.UTF8EncodingNoPrefix for all encoding, unless you have specific needs.
 



