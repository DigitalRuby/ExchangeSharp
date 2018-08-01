Pull requests are encouraged and welcome. The more people contributing, the more features and faster this API will grow, serving everyone who uses C# and cryptocurrency.

Please follow these coding guidelines...
- For JSON parsing and strings, please use the ```CryptoUtility``` extension methods ```ToStringInvariant``` and ```ConvertInvariant``` for all parsing or conversion to other values. This ensures that any locale machine will have no problems parsing and converting data. Do not use .Value<T> functions.
- Please use the CryptoUtility extension methods for string utf8 encode / decode, or for DateTime conversions.
- Always, always search for existing code in the project before coding new functions. CryptoUtility has a lot of useful stuff.
- Keep the code simple and the project lean. We do NOT want to turn into https://github.com/knowm/XChange.
- Before adding NuGet packages, consider copying in the bits of the source that matter into the Dependencies folder. The fewer packages that ExchangeSharp depends on, the better.
- Follow these code style guidelines please (we're not monsters):
  - Tabs for indent.
  - Curly braces on separate lines.
  - Wrap all if statements with curly braces, makes debug and set breakpoints much easier, along with adding new code to the if statement block.

When creating a new Exchange API, please do the following:
- For reference comparisons, https://github.com/ccxt/ccxt is a good project to compare against when creating a new exchange.
- Add the new exchange name to the ExchangeName class.
- Each exchange API class is in it's own folder. If you are creating model objects or helper classes for an exchange, make internal classes inside a namespace for your exchange and put them in the sub-folder for the exchange. Binance and Bittrex are good examples.
- Override the protected methods of ExchangeAPI that you want to implement. Easiest way is find another exchange and copy the file and customize as needed.
- Set additional protected and public properties in constructor as needed (SymbolSeparator, SymbolIsReversed, SymbolIsUppercase, etc.).
- If the exchange uses funny currency names (i.e. BCC instead of BCH), set up a map in the static constructor. See ExchangeYobitAPI.cs for an example.
- Ensure that the unit tests and integrations tests (ExchangeSharpConsole.exe test exchangeName=[name]) pass before submitting a pull request.
 



