using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

// ReSharper disable once CheckNamespace
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
	/// <summary>
	/// An extension to the [Ignore] attribute. Instead of using test lists / test categories to conditionally
	/// skip tests, allow a [TestClass] or [TestMethod] to specify a method to run. If the member returns
	/// `true` the test method will be skipped. The "ignore criteria" method or property must be `static`, return a single
	/// `bool` value, and not accept any parameters.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public class IgnoreIfAttribute : Attribute
	{
		public string IgnoreCriteriaMemberName { get; }

		public string Message { get; }

		public IgnoreIfAttribute(string ignoreCriteriaMemberName, string message = null)
		{
			IgnoreCriteriaMemberName = ignoreCriteriaMemberName;
			Message = message;
		}

		internal virtual bool ShouldIgnore(ITestMethod testMethod)
		{
			try
			{
				// Search for the method or prop specified by name in this class or any parent classes.
				var searchFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy |
				                  BindingFlags.Static;
				Debug.Assert(testMethod.MethodInfo.DeclaringType != null,
					"testMethod.MethodInfo.DeclaringType != null");
				var member = testMethod.MethodInfo.DeclaringType.GetMember(IgnoreCriteriaMemberName, searchFlags)
					.FirstOrDefault();

				switch (member)
				{
					case MethodInfo method:
						return (bool) method.Invoke(null, null);
					case PropertyInfo prop:
						return (bool) prop.GetValue(null);
					default:
						throw new ArgumentOutOfRangeException(nameof(member));
				}
			}
			catch (Exception e)
			{
				var message =
					$"Conditional ignore bool returning method/prop {IgnoreCriteriaMemberName} not found. Ensure the method/prop is in the same class as the test method, marked as `static`, returns a `bool`, and doesn't accept any parameters.";
				throw new ArgumentException(message, e);
			}
		}
	}
}
