using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvGet.Entities;
using NvGet.Tools.Updater.Entities;
using NvGet.Tools.Updater.Extensions;
using NuGet.Versioning;

namespace NvGet.Tests.Tools.Updater
{
	[TestClass]
	public class FallbackMappingsTests
	{
		[TestMethod]
		public async Task GivenOutdatedBasePackage_FallbackIsUsed()
		{
			// Simulate scenario where base package "uno.extensions" has outdated version (3.0.0)
			// but fallback "Uno.Extensions.Core" has newer version (7.1.0)
			var testPackages = new System.Collections.Generic.Dictionary<string, string[]>
			{
				{ "uno.extensions", new[] { "3.0.0-dev.1957" } },
				{ "Uno.Extensions.Core", new[] { "7.1.0-dev.62" } },
			};

			var testFeed = new NvGet.Tools.Tests.Entities.TestPackageFeed(Constants.TestFeedUri, testPackages);

			var reference = new PackageReference("uno.extensions", "3.0.0-dev.1957");

			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { testFeed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			// Should use the fallback package's newer version
			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("7.1.0-dev.62"), version.Version);
		}

		[TestMethod]
		public async Task GivenNewerBasePackage_FallbackIsNotUsed()
		{
			// Simulate scenario where base package has newer version than fallback
			var testPackages = new System.Collections.Generic.Dictionary<string, string[]>
			{
				{ "uno.extensions", new[] { "8.0.0-dev.100" } },
				{ "Uno.Extensions.Core", new[] { "7.1.0-dev.62" } },
			};

			var testFeed = new NvGet.Tools.Tests.Entities.TestPackageFeed(Constants.TestFeedUri, testPackages);

			var reference = new PackageReference("uno.extensions", "3.0.0-dev.1957");

			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { testFeed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			// Should use the base package's newer version
			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("8.0.0-dev.100"), version.Version);
		}

		[TestMethod]
		public async Task GivenCustomFallbackMapping_OverridesWellKnown()
		{
			// Test that custom mappings in PropertyPackageFallbackMappings take precedence
			var testPackages = new System.Collections.Generic.Dictionary<string, string[]>
			{
				{ "uno.extensions", new[] { "3.0.0-dev.1957" } },
				{ "Uno.Extensions.Core", new[] { "7.1.0-dev.62" } },
				{ "Custom.Extensions.Package", new[] { "9.0.0-dev.1" } },
			};

			var testFeed = new NvGet.Tools.Tests.Entities.TestPackageFeed(Constants.TestFeedUri, testPackages);

			var reference = new PackageReference("uno.extensions", "3.0.0-dev.1957");

			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { testFeed },
				PropertyPackageFallbackMappings = 
				{
					{ "uno.extensions", "Custom.Extensions.Package" }
				}
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			// Should use the custom fallback package
			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("9.0.0-dev.1"), version.Version);
		}

		[TestMethod]
		public async Task GivenUnoMaterial_FallbackToWinUI()
		{
			// Test Uno.Material -> Uno.Material.WinUI fallback
			var testPackages = new System.Collections.Generic.Dictionary<string, string[]>
			{
				{ "uno.material", new[] { "5.8.0-dev.2" } },
				{ "Uno.Material.WinUI", new[] { "6.1.0-dev.4" } },
			};

			var testFeed = new NvGet.Tools.Tests.Entities.TestPackageFeed(Constants.TestFeedUri, testPackages);

			var reference = new PackageReference("uno.material", "5.8.0-dev.2");

			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { testFeed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			// Should use Uno.Material.WinUI's newer version
			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("6.1.0-dev.4"), version.Version);
		}

		[TestMethod]
		public async Task GivenNoFallbackMapping_UsesBasePackage()
		{
			// Test that packages without fallback mappings work normally
			var testPackages = new System.Collections.Generic.Dictionary<string, string[]>
			{
				{ "Uno.UI", new[] { "2.1.39", "2.2.0", "3.0.0-dev.2" } },
			};

			var testFeed = new NvGet.Tools.Tests.Entities.TestPackageFeed(Constants.TestFeedUri, testPackages);

			var reference = new PackageReference("Uno.UI", "2.1.39");

			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { testFeed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("3.0.0-dev.2"), version.Version);
		}
	}
}
