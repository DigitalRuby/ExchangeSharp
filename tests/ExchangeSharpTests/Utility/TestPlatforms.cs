using System;

// ReSharper disable once CheckNamespace
namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
	[Flags]
	public enum TestPlatforms
	{
		Windows = 1,
		Linux = 2,
		OSX = 4,
		FreeBSD = 8,
		NetBSD = 16,
		AnyUnix = FreeBSD | Linux | NetBSD | OSX,
		Any = ~0
	}
}
