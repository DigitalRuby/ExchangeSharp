Pull requests are encouraged and welcome. The more people contributing, the more features and faster this API will grow, serving everyone who uses C# and cryptocurrency.

Please follow these guidelines...
- For JSON parsing and strings, please use the ```CryptoUtility``` extension methods ```ToStringInvariant``` and ```ConvertInvariant``` for all parsing or conversion to other values. This ensures that any locale machine will have no problems parsing and converting data.
- When creating a new Exchange API, please do the following:
  - Override the protected methods of ExchangeAPI that you want to implement.
  - Ensure that the unit tests and integrations tests (ExchangeSharpConsole.exe test exchangeName=[name]) pass before submitting a pull request.
  - Easiest way is find another exchange and copy the file and customize as needed.


