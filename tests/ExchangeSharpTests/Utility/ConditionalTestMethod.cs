using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
	/// <summary>
	/// An extension to the [TestMethod] attribute. It walks the method hierarchy looking
	/// for an [IgnoreIf] attribute. If one or more are found, they are each evaluated, if the attribute
	/// returns `true`, evaluation is short-circuited, and the test method is skipped.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public class ConditionalTestMethodAttribute : TestMethodAttribute
	{
		public override TestResult[] Execute(ITestMethod testMethod)
		{
			var ignoreAttributes = FindAttributes(testMethod);

			// Evaluate each attribute, and skip if one returns `true`
			foreach (var ignoreAttribute in ignoreAttributes)
			{
				if (!ignoreAttribute.ShouldIgnore(testMethod))
					continue;

				var message =
					"Test not executed. " +
					(string.IsNullOrWhiteSpace(ignoreAttribute.Message)
						? $"Conditionally ignored by {ignoreAttribute.GetType().Name}."
						: ignoreAttribute.Message);

				return new[]
				{
					new TestResult
					{
						Outcome = UnitTestOutcome.Inconclusive,
						TestFailureException = new AssertInconclusiveException(message)
					}
				};
			}

			return base.Execute(testMethod);
		}

		private IEnumerable<IgnoreIfAttribute> FindAttributes(ITestMethod testMethod)
		{
			// Look for an [IgnoreIf] on the method, including any virtuals this method overrides
			var ignoreAttributes = new List<IgnoreIfAttribute>();

			ignoreAttributes.AddRange(testMethod.GetAttributes<IgnoreIfAttribute>(inherit: true));

			return ignoreAttributes;
		}
	}
}
