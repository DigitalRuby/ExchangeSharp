using System;
using System.Threading.Tasks;
using CommandLine;
using ExchangeSharp;

namespace ExchangeSharpConsole.Options
{
	[Verb("keys", HelpText = "Encrypted API Key File Utility.\n" +
	                         "This file is only valid for the current user and only on the computer it is created on.")]
	public class KeysOption : BaseOption
	{
		public override async Task RunCommand()
		{
			if (string.IsNullOrWhiteSpace(Path))
			{
				throw new ArgumentException("Invalid path.", nameof(Path));
			}

			if (Mode.Equals("create", StringComparison.OrdinalIgnoreCase))
			{
				await CreateKeyFile();
			}
			else if (Mode.Equals("display", StringComparison.OrdinalIgnoreCase))
			{
				DisplayKeyContents();
			}
			else
			{
				throw new ArgumentException($"Invalid mode: {Mode}");
			}
		}

		private void DisplayKeyContents()
		{
			Console.WriteLine($"Reading file with keys in \"{System.IO.Path.GetFullPath(Path)}\".");

			var secureStrings = CryptoUtility.LoadProtectedStringsFromFile(Path);

			foreach (var s in secureStrings)
			{
				Console.WriteLine(s.ToUnsecureString());
			}
		}

		private async Task CreateKeyFile()
		{
			var keyList = await ReadKeysAsync();

			CryptoUtility.SaveUnprotectedStringsToFile(Path, keyList);

			Console.WriteLine($"Created file in \"{System.IO.Path.GetFullPath(Path)}\".");
		}

		private async Task<string[]> ReadKeysAsync()
		{
			if (ReadKeyFromStdin)
			{
				var stdinData = await Console.In.ReadToEndAsync();
				return stdinData.Split(Environment.NewLine);
			}

			if (string.IsNullOrWhiteSpace(KeyList))
			{
				throw new ArgumentException("The argument key-list is empty.");
			}

			return KeyList.Split(',');
		}

		[Option('m', "mode", Required = true,
			HelpText = "Mode of execution. \n" +
			           "\tPossible values are \"create\" or \"display\"." +
			           "\t\tcreate: Creates a protected storage for public and private keys." +
			           "\t\tdisplay: Displays the protected pair.")]
		public string Mode { get; set; }

		[Option("key-list", SetName = "key-interactive",
			HelpText = "Comma separated list of keys to be stored.")]
		public string KeyList { get; set; }

		[Option("key-stdin", SetName = "key-not-interactive",
			HelpText = "Switch to enable reading the key from the stdin.")]
		public bool ReadKeyFromStdin { get; set; }

		[Option('p', "path", Default = "keys.bin",
			HelpText = "Where the data will be stored or read from.")]
		public string Path { get; set; }
	}
}
