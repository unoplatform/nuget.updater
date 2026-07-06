using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Versioning;
using NvGet.Entities;
using NvGet.Tools.Tests.Entities;
using NvGet.Tools.Updater.Entities;
using NvGet.Tools.Updater.Extensions;

namespace NvGet.Tests.Tools.Updater
{
	/// <summary>
	/// End-to-end coverage for the well-known property→package fallback resolution
	/// in <see cref="UpdaterParametersExtension.GetLatestVersion(UpdaterParameters, CancellationToken, PackageReference)"/>.
	///
	/// Real-world cases mirror canary build observations:
	///  • Studio: UnoMaterialVersion / UnoExtensionsVersion silently skipped because the
	///    name-derived base package (uno.material / uno.extensions) is stale on NuGet.org.
	/// </summary>
	[TestClass]
	public class PropertyPackageFallbackTests
	{
		[TestMethod]
		public async Task GivenOutdatedUnoExtensionsBase_FallbackPicksUnoExtensionsCore()
		{
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "uno.extensions", new[] { "3.0.0-dev.1957" } },        // stale legacy umbrella
				{ "Uno.Extensions.Core", new[] { "7.3.0-dev.78" } },     // current modern package
			});

			var reference = new PackageReference("uno.extensions", "7.0.17");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version, "Fallback should have produced a version.");
			Assert.AreEqual(NuGetVersion.Parse("7.3.0-dev.78"), version.Version);
		}

		[TestMethod]
		public async Task GivenOutdatedUnoMaterialBase_FallbackPicksUnoMaterialWinUI()
		{
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "uno.material", new[] { "5.8.0-dev.2" } },           // stale legacy UWP package
				{ "Uno.Material.WinUI", new[] { "7.0.0-dev.33" } },    // current modern package
			});

			var reference = new PackageReference("uno.material", "7.0.0-dev.28");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("7.0.0-dev.33"), version.Version);
		}

		[TestMethod]
		public async Task GivenOutdatedUnoThemesBase_FallbackPicksUnoThemesWinUI()
		{
			// Toolkit canary scenario: <UnoThemesVersion>7.0.0-dev.29</UnoThemesVersion> with
			// <PackageVersion Include="Uno.Themes.WinUI" Version="$(UnoThemesVersion)" />.
			// Without fallback the updater queries `uno.themes` (legacy 5.x) and silently skips.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "uno.themes", new[] { "5.8.0-dev.2" } },           // stale legacy package
				{ "Uno.Themes.WinUI", new[] { "7.0.0-dev.33" } },    // current modern package
			});

			var reference = new PackageReference("uno.themes", "7.0.0-dev.29");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("7.0.0-dev.33"), version.Version);
		}

		[TestMethod]
		public async Task GivenBasePackageNewerThanFallback_FallbackIsNotPreferred()
		{
			// If the upstream ever revives the legacy base package and ships newer than the WinUI variant,
			// we should still pick the higher version.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "uno.material", new[] { "8.0.0-dev.10" } },          // hypothetically revived
				{ "Uno.Material.WinUI", new[] { "7.0.0-dev.33" } },
			});

			var reference = new PackageReference("uno.material", "5.0.0");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("8.0.0-dev.10"), version.Version);
		}

		[TestMethod]
		public async Task GivenLocalEqualsStandard_FallbackDoesNotFire()
		{
			// Boundary of the suspicion gate: local == standard. Gate is strict (local > standard),
			// so equality must NOT trigger the fallback. The standard lookup wins (returns the same
			// version, no actual upgrade). This guards against a future off-by-one bug where someone
			// might "tidy" the gate to >=.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "uno.material", new[] { "5.8.0-dev.2" } },
				{ "Uno.Material.WinUI", new[] { "7.0.0-dev.33" } },
			});

			var reference = new PackageReference("uno.material", "5.8.0-dev.2");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("5.8.0-dev.2"), version.Version);
		}

		[TestMethod]
		public async Task GivenLocalBehindStandard_FallbackDoesNotFire()
		{
			// Toolkit-style scenario: direct <PackageVersion Include="Uno.Material" Version="5.0.13" />
			// (no property indirection). The legacy line is still alive at 5.8.0-dev.2 and we want to
			// bump within the legacy line — NOT silently swap in Uno.Material.WinUI 7.x, which
			// would write a version that doesn't exist for the original package ID and break restore.
			//
			// Suspicion gate: local (5.0.13) is NOT > standard (5.8.0-dev.2), so the fallback is skipped.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "uno.material", new[] { "5.0.13", "5.8.0-dev.2" } },     // legacy line still alive
				{ "Uno.Material.WinUI", new[] { "7.0.0-dev.33" } },         // modern line ahead
			});

			var reference = new PackageReference("uno.material", "5.0.13");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("5.8.0-dev.2"), version.Version);
		}

		[TestMethod]
		public async Task GivenPackageWithoutMapping_NoFallbackAttempted()
		{
			// Sanity: packages without a well-known mapping behave exactly as before.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "Uno.UI", new[] { "2.1.39", "2.2.0", "3.0.0-dev.2" } },
			});

			var reference = new PackageReference("Uno.UI", "2.1.39");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("3.0.0-dev.2"), version.Version);
		}

		[TestMethod]
		public async Task GivenBothBaseAndFallbackAbsent_ReturnsNull()
		{
			// Edge case: neither the heuristic-derived package nor the fallback target exist on the feed.
			// Should return null (no version available), not throw or return a stale value.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "SomeOtherPackage", new[] { "1.0.0" } },
				// Neither uno.material nor Uno.Material.WinUI in the feed.
			});

			var reference = new PackageReference("uno.material", "7.0.0-dev.28");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNull(version);
		}

		[TestMethod]
		public async Task GivenForcedVersionOverride_FallbackIsNotConsulted()
		{
			// Contract: --versionOverrides=file.json with `"UpdatedVersion": "X"` short-circuits
			// the entire lookup before either standard or fallback resolution. The fallback must
			// never override a forced version. Guards against a future refactor that reorders
			// these checks.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "uno.material", new[] { "5.8.0-dev.2" } },
				{ "Uno.Material.WinUI", new[] { "7.0.0-dev.33" } },
			});

			var reference = new PackageReference("uno.material", "7.0.0-dev.28");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};
			// Force-pin uno.material to a specific version that exists nowhere else.
			parameters.VersionOverrides.Add("uno.material",
				(forceVersion: true,
				 upgradePolicy: UpgradePolicy.Major,
				 range: new VersionRange(
					minVersion: NuGetVersion.Parse("6.6.6-pinned"),
					includeMinVersion: true,
					maxVersion: NuGetVersion.Parse("6.6.6-pinned"),
					includeMaxVersion: true,
					floatRange: null,
					originalString: null)));

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("6.6.6-pinned"), version.Version);
		}

		[TestMethod]
		public async Task GivenBaseReturnsNothing_FallbackStillResolves()
		{
			// Edge case: legacy package not present on the feed at all.
			// Fallback should still resolve a version.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "Uno.Material.WinUI", new[] { "7.0.0-dev.33" } },
				// uno.material intentionally missing
			});

			var reference = new PackageReference("uno.material", "7.0.0-dev.28");
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, reference);

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("7.0.0-dev.33"), version.Version);
		}

	}
}
