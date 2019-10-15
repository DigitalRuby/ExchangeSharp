using System.Threading.Tasks;

namespace ExchangeSharpConsole.Options
{
	public abstract class BaseOption
	{
		public abstract Task RunCommand();
	}
}
