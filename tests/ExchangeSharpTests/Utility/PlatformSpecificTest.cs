using System;
using System.Runtime.InteropServices;

// ReSharper disable once CheckNamespace
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public class PlatformSpecificTestAttribute : IgnoreIfAttribute
	{
		public static OSPlatform NetBSD { get; } = OSPlatform.Create("NETBSD");

		public TestPlatforms FlagPlatform { get; }

		public PlatformSpecificTestAttribute(TestPlatforms flagPlatform, string message = null)
			: base(null, message)
		{
			FlagPlatform = flagPlatform;
		}

		internal override bool ShouldIgnore(ITestMethod testMethod)
		{
			var shouldRun = false;

			if (FlagPlatform.HasFlag(TestPlatforms.Any))
				return true;
			if (FlagPlatform.HasFlag(TestPlatforms.Windows))
				shouldRun = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
			if (FlagPlatform.HasFlag(TestPlatforms.Linux))
				shouldRun = shouldRun || RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
			if (FlagPlatform.HasFlag(TestPlatforms.OSX))
				shouldRun = shouldRun || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
			if (FlagPlatform.HasFlag(TestPlatforms.FreeBSD))
				shouldRun = shouldRun || RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);
			if (FlagPlatform.HasFlag(TestPlatforms.NetBSD))
				shouldRun = shouldRun || RuntimeInformation.IsOSPlatform(NetBSD);

			return !shouldRun;
		}
	}
}
