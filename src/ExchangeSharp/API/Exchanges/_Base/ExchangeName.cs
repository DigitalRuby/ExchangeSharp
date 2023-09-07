/*
MIT LICENSE

Copyright 2017 Digital Ruby, LLC - http://www.digitalruby.com

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ExchangeSharp
{
	/// <summary>
	/// List of exchange names
	/// Note: When making a new exchange, add a partial class underneath the exchange class with the name, decouples
	/// the names from a global list here and keeps them with each exchange class.
	/// </summary>
	public static partial class ExchangeName
	{
		private static readonly Type exchangeApiType = typeof(ExchangeAPI);
		private static readonly HashSet<string> exchangeNames = new HashSet<string>();

		static ExchangeName()
		{
			foreach (
					FieldInfo field in typeof(ExchangeName).GetFields(
							BindingFlags.Public | BindingFlags.Static
					)
			)
			{
				// pull value of name field
				string name = field.GetValue(null)!.ToString();

				// make sure we have a valid type for the name
				_ = GetExchangeType(name);

				// add to unique list of names
				exchangeNames.Add(name);
			}
		}

		internal static Type GetExchangeType(string exchangeName)
		{
			try
			{
				// make sure we have a valid type for the name
				Type type = Type.GetType($"ExchangeSharp.Exchange{exchangeName}API", true, true);

				// we had better have a type sub-classing from ExchangeAPI
				if (type is null || !type.IsSubclassOf(exchangeApiType))
				{
					throw new ApplicationException(
							$"Name of {exchangeName} is not an {nameof(ExchangeAPI)} class"
					);
				}
				return type;
			}
			catch (Exception ex)
			{
				// fatal
				throw new ApplicationException(
						$"Failed to get type from exchange name {exchangeName}",
						ex
				);
			}
		}

		/// <summary>
		/// Check if an exchange name exists
		/// </summary>
		/// <param name="name">Name</param>
		/// <returns>True if name exists, false otherwise</returns>
		public static bool HasName(string name)
		{
			return exchangeNames.Contains(name);
		}

		/// <summary>
		/// Get a list of all exchange names
		/// </summary>
		public static IReadOnlyCollection<string> ExchangeNames
		{
			get { return exchangeNames; }
		}
	}
}
