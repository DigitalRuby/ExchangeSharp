Pull requests are encouraged and welcome. The more people contributing, the more features and faster this API will grow, serving everyone who uses C# and cryptocurrency.

Please follow these coding guidelines...
- For JSON parsing and strings, please use the ```CryptoUtility``` extension methods ```ToStringInvariant``` and ```ConvertInvariant``` for all parsing or conversion to other values. This ensures that any locale machine will have no problems parsing and converting data. Do not use ```.Value<T>``` functions.
- Please use the ```CryptoUtility``` extension methods for string utf8 encode / decode, or for ```DateTime``` conversions.
- Always, always search for existing code in the project before coding new functions. ```CryptoUtility``` has a lot of useful stuff.
- Keep the code simple and the project lean. We do NOT want to turn into https://github.com/knowm/XChange.
- Avoid passing hard-coded JSON strings when writing to socket or http. Create a ```var obj = new {}``` and use that instead.
- Before adding NuGet packages, consider copying in the bits of the source that matter into the Dependencies folder. The fewer packages that ExchangeSharp depends on, the better.
- Only implement async methods as a general rule. Synchronous calls can be done by using the Sync extension method in ```CryptoUtility```. Saves a lot of duplicate code.
- Use CryptoUtility.UtcNow instead of DateTime.UtcNow. This makes it easy to mock the date and time for unit or integration tests.
- Use CryptoUtility.UnixTimeStampToDateTimeSeconds and CryptoUtility.UnixTimestampFromDateTimeSeconds, etc. for date conversions, keep all date in UTC.
- Please follow these code style guidelines please (we're not monsters):
  - Tabs for indent.
  - Curly braces on separate lines.
  - Wrap if, else if, and any loops or control logic with curly braces. Avoid `if (something) doSomething();` on same line or next line without curly braces.
  - Append 'Async' to the end of the name of any method returning a Task - except for unit and integration test methods.
  - Avoid using `.Sync()` or `.RunSynchronously()` - all code should use Tasks and/or async / await. Exception would be console or other non-library projects.
- Avoid `ConfigureAwait(false)`. Most API methods are wrapped by a synchronization context remover, so this should not be necessary in almost all cases.
 
When creating a new Exchange API, please do the following:
- For reference comparisons, https://github.com/ccxt/ccxt is a good project to compare against when creating a new exchange. Use node.js in Visual Studio to debug through the code.
- See ```ExchangeAPIDefinitions.cs``` for all possible methods that can be overriden to make an exchange, along with adding the name to the ExchangeName class. Great starting point to copy/paste as your new Exchange...API.cs file.
- Put the exchange API class is in it's own folder (/API/Exchanges). If you are creating model objects or helper classes for an exchange, make internal classes inside a namespace for your exchange and put them in the sub-folder for the exchange. Binance and Bittrex are good examples.
- Please use ```CryptoUtility, BaseAPIExtensions and ExchangeAPIExtensions``` for common code / parsing before rolling your own parsing code.
- Ensure that the unit tests and integrations tests (```ExchangeSharpConsole.exe test exchangeName=[name]```) pass before submitting a pull request.
- Set needed properties in the constructor of the exchange, such as NonceStyle, NonceEndPoint, NonceEndPointField, NonceEndPointStyle and any property in /API/Exchanges/_Base/ExchangeAPIDefinitions.cs.
- If you have very custom nonce offset calculation logic, you can override OnGetNonceOffset, but please do only if absolutely necessary.
 



