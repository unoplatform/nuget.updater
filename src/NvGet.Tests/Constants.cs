using System;
using System.Collections.Generic;
using System.Text;
using NvGet.Tools.Tests.Entities;

namespace NvGet.Tests
{
	public static class Constants
	{
		public static readonly Uri TestFeedUri = new Uri("http://localhost");

		public static readonly Dictionary<string, string[]> TestPackages = new Dictionary<string, string[]>
		{
			{"unoplatform.NuGet.Updater", new[] { "1.0-beta.1" } },
			{"Uno.UI", new[] { "2.1.39", "2.2.0", "2.3.0-dev.44", "2.3.0-dev.48", "2.3.0-dev.58", "3.0.0-dev.2", "5.0.0-feature.5x.88" } },
		};

		public static readonly TestPackageFeed TestFeed = new TestPackageFeed(TestFeedUri, TestPackages);

		public static readonly Dictionary<string, string[]> TestPackages2 = new Dictionary<string, string[]>
		{
			{"Uno.UI", new[] { "2.1.39", "2.2.0", "2.2.1", "2.3.0" } },
		};

		public static readonly TestPackageFeed TestFeed2 = new TestPackageFeed(TestFeedUri, TestPackages2);
	}
}
