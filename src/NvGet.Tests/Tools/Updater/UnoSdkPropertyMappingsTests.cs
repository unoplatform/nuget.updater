using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NvGet.Contracts;
using NvGet.Entities;
using NvGet.Helpers;
using NvGet.Tools.Tests.Entities;
using NvGet.Tools.Updater.Entities;
using NvGet.Tools.Updater.Extensions;

namespace NvGet.Tests.Tools.Updater
{
	/// <summary>
	/// Version properties the name-based guess can't resolve: the built-in list for the current
	/// Uno.Sdk properties, and the "versionProperty" field the Uno.Sdk publishes for new ones.
	/// </summary>
	[TestClass]
	public class UnoSdkPropertyMappingsTests
	{
		// Shape of the Uno.Sdk packages.json with the versionProperty field (unoplatform/uno#26611).
		private const string SdkManifest = """
			[
				{ "group": "Core", "version": "DefaultUnoVersion", "packages": [ "Uno.WinUI" ] },
				{ "group": "CSharpMarkup", "version": "7.0.1", "packages": [ "Uno.WinUI.Markup", "Uno.Extensions.Markup.Generators" ], "versionProperty": "UnoCSharpMarkupVersion" },
				{ "group": "NewThing", "version": "1.0.0", "packages": [ "Uno.New.Thing" ], "versionProperty": "UnoNewThingVersion" },
				{ "group": "SkiaSharp", "version": "4.156.1", "packages": [ "SkiaSharp.Skottie" ], "versionProperty": "SkiaSharpVersion" },
				{ "group": "Maui", "version": "10.0.120", "packages": [ "Microsoft.Maui.Controls" ], "versionProperty": "MauiVersion" }
			]
			""";

		[DataTestMethod]
		// Every Uno.Sdk property whose name-derived ID matches no package (nuget.updater#45).
		[DataRow("UnoCSharpMarkupVersion", "uno.csharp.markup", "Uno.WinUI.Markup")]
		[DataRow("UnoCoreLoggingSingletonVersion", "uno.core.logging.singleton", "Uno.Core.Extensions.Logging.Singleton")]
		[DataRow("UnoFontsVersion", "uno.fonts", "Uno.Fonts.OpenSans")]
		[DataRow("UnoHotDesignVersion", "uno.hot.design", "Uno.UI.HotDesign")]
		[DataRow("UnoLoggingVersion", "uno.logging", "Uno.Extensions.Logging.OSLog")]
		[DataRow("UnoMauiVersion", "uno.maui", "Uno.Maui.Embedding")]
		[DataRow("UnoReactorVersion", "uno.reactor", "Uno.Microsoft.UI.Reactor")]
		[DataRow("UnoRetargetVersion", "uno.retarget", "Uno.WinUI.Retarget")]
		[DataRow("UnoSettingsVersion", "uno.settings", "Uno.Settings.DevServer")]
		[DataRow("UnoAppMcpVersion", "uno.app.mcp", "Uno.UI.App.Mcp")]
		public async Task GivenUnoSdkProperty_BuiltInMappingResolvesTheRealPackage(string propertyName, string derivedId, string packageId)
		{
			Assert.AreEqual(derivedId, VersionPropertyNames.ToPackageId(propertyName));

			// The derived ID doesn't exist; only the real package does.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ packageId, new[] { "1.0.0", "2.0.0-dev.5" } },
			});
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
				UnoSdkMappings = UnoSdkPropertyMappings.Empty,
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, new PackageReference(derivedId, "1.0.0"));

			Assert.IsNotNull(version, $"{propertyName} should resolve through {packageId}.");
			Assert.AreEqual(NuGetVersion.Parse("2.0.0-dev.5"), version.Version);
		}

		[TestMethod]
		public void Parse_KeepsUnoGroupsThatNameTheirProperty()
		{
			var mappings = UnoSdkPropertyMappings.Parse(SdkManifest, "test");

			Assert.AreEqual(2, mappings.Count, "Core has no property; SkiaSharp and Maui are not Uno packages.");

			Assert.IsTrue(mappings.TryGetPackageId("uno.csharp.markup", out var packageId, out var propertyName));
			Assert.AreEqual("Uno.WinUI.Markup", packageId);
			Assert.AreEqual("UnoCSharpMarkupVersion", propertyName);

			Assert.IsTrue(mappings.TryGetPackageId("Uno.New.Thing".ToLowerInvariant(), out packageId, out _));
			Assert.AreEqual("Uno.New.Thing", packageId);

			Assert.IsFalse(mappings.TryGetPackageId("skia.sharp", out _, out _));
			Assert.IsFalse(mappings.TryGetPackageId("maui", out _, out _));
		}

		[TestMethod]
		public void Parse_OlderSdkWithoutTheField_IsEmpty()
		{
			var mappings = UnoSdkPropertyMappings.Parse("""[ { "group": "CSharpMarkup", "version": "7.0.1", "packages": [ "Uno.WinUI.Markup" ] } ]""", "test");

			Assert.AreEqual(0, mappings.Count);
		}

		[TestMethod]
		public void Parse_AcceptsAByteOrderMark()
		{
			var mappings = UnoSdkPropertyMappings.Parse("﻿" + SdkManifest, "test");

			Assert.AreEqual(2, mappings.Count);
		}

		[TestMethod]
		public async Task GivenNewSdkProperty_UnoSdkMappingResolvesIt_WithoutAnUpdaterChange()
		{
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "Uno.New.Thing", new[] { "1.0.0", "1.1.0-dev.3" } },
			});
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
				UnoSdkMappings = UnoSdkPropertyMappings.Parse(SdkManifest, "Uno.Sdk.Private test"),
			};

			// <UnoNewThingVersion>1.0.0</UnoNewThingVersion> → uno.new.thing, which isn't a package.
			var version = await parameters.GetLatestVersion(CancellationToken.None, new PackageReference("uno.new.thing", "1.0.0"));

			Assert.IsNotNull(version);
			Assert.AreEqual(NuGetVersion.Parse("1.1.0-dev.3"), version.Version);
		}

		[TestMethod]
		public async Task GivenPropertyNobodyMaps_StillSkipped()
		{
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>());
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
				UnoSdkMappings = UnoSdkPropertyMappings.Parse(SdkManifest, "test"),
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, new PackageReference("application.display", "1.0"));

			Assert.IsNull(version);
		}

		[TestMethod]
		public async Task GivenNameDerivedPackageIsCurrent_MappingsAreNotConsulted()
		{
			// Same rule as 1.3.1: when the guessed package exists and is newer, it wins.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "uno.new.thing", new[] { "3.0.0-dev.1" } },
				{ "Uno.New.Thing", new[] { "9.0.0-dev.1" } },
			});
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev" },
				Feeds = { feed },
				UnoSdkMappings = UnoSdkPropertyMappings.Parse(SdkManifest, "test"),
			};

			var version = await parameters.GetLatestVersion(CancellationToken.None, new PackageReference("uno.new.thing", "2.0.0"));

			Assert.AreEqual(NuGetVersion.Parse("3.0.0-dev.1"), version.Version);
		}

		[TestMethod]
		public async Task GivenOverrideOnTheMappedPackage_ItAppliesToTheProperty()
		{
			// Canaries cap packages by their real ID (uno.extensions caps Uno.Extensions.Logging.OSLog at 1.7.0
			// because 1.9.0-dev.2 is stale). A property mapped to that package must get the same cap.
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "Uno.Extensions.Logging.OSLog", new[] { "1.7.0", "1.9.0-dev.2" } },
			});
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev", "stable" },
				Feeds = { feed },
				UnoSdkMappings = UnoSdkPropertyMappings.Empty,
			};
			parameters.VersionOverrides["Uno.Extensions.Logging.OSLog"] = (true, UpgradePolicy.Major, new VersionRange(NuGetVersion.Parse("1.7.0")));

			var version = await parameters.GetLatestVersion(CancellationToken.None, new PackageReference("uno.logging", "1.6.0"));

			Assert.AreEqual(NuGetVersion.Parse("1.7.0"), version.Version);
		}

		[TestMethod]
		public async Task GivenRangeOverrideOnTheMappedPackage_ItLimitsTheProperty()
		{
			var feed = new TestPackageFeed(Constants.TestFeedUri, new Dictionary<string, string[]>
			{
				{ "Uno.WinUI.Markup", new[] { "7.0.3", "7.1.0-dev.5", "8.0.0-dev.1" } },
			});
			var parameters = new UpdaterParameters
			{
				TargetVersions = { "dev", "stable" },
				Feeds = { feed },
				UnoSdkMappings = UnoSdkPropertyMappings.Empty,
			};
			parameters.VersionOverrides["Uno.WinUI.Markup"] = (false, UpgradePolicy.Major, VersionRange.Parse("[7.0.0,8.0.0-dev)"));

			var version = await parameters.GetLatestVersion(CancellationToken.None, new PackageReference("uno.csharp.markup", "7.0.1"));

			Assert.AreEqual(NuGetVersion.Parse("7.1.0-dev.5"), version.Version);
		}

		[TestMethod]
		public async Task LoadUnoSdkMappings_ReadsTheLatestUnoSdkPrivate()
		{
			var feed = new SdkPackageFeed(new Dictionary<string, string>
			{
				{ "7.1.0-dev.100", """[ { "group": "Old", "version": "1.0.0", "packages": [ "Uno.Old" ], "versionProperty": "UnoOldVersion" } ]""" },
				{ "7.1.0-dev.200", SdkManifest },
			});
			var parameters = new UpdaterParameters { TargetVersions = { "dev" }, Feeds = { feed } };

			var mappings = await UpdaterParametersExtension.LoadUnoSdkMappings(parameters, CancellationToken.None);

			Assert.AreEqual("Uno.Sdk.Private 7.1.0-dev.200", mappings.Source);
			Assert.IsTrue(mappings.TryGetPackageId("uno.new.thing", out var packageId, out _));
			Assert.AreEqual("Uno.New.Thing", packageId);
			Assert.IsFalse(mappings.TryGetPackageId("uno.old", out _, out _));
		}

		[TestMethod]
		public async Task LoadUnoSdkMappings_FollowsAForcedUnoSdkPrivateOverride()
		{
			var feed = new SdkPackageFeed(new Dictionary<string, string>
			{
				{ "7.0.29", """[ { "group": "Old", "version": "1.0.0", "packages": [ "Uno.Old" ], "versionProperty": "UnoOldVersion" } ]""" },
				{ "7.1.0-dev.200", SdkManifest },
			});
			var parameters = new UpdaterParameters { TargetVersions = { "dev", "stable" }, Feeds = { feed } };
			parameters.VersionOverrides["Uno.Sdk.Private"] = (true, UpgradePolicy.Major, new VersionRange(NuGetVersion.Parse("7.0.29")));

			var mappings = await UpdaterParametersExtension.LoadUnoSdkMappings(parameters, CancellationToken.None);

			Assert.IsTrue(mappings.TryGetPackageId("uno.old", out _, out _));
		}

		[TestMethod]
		public async Task LoadUnoSdkMappings_NeverFails()
		{
			var parameters = new UpdaterParameters { TargetVersions = { "dev" }, Feeds = { new SdkPackageFeed(new Dictionary<string, string>(), failDownload: true) } };
			Assert.AreEqual(0, (await UpdaterParametersExtension.LoadUnoSdkMappings(parameters, CancellationToken.None)).Count, "No Uno.Sdk.Private on the feed.");

			parameters = new UpdaterParameters { TargetVersions = { "dev" }, Feeds = { new SdkPackageFeed(new Dictionary<string, string> { { "7.1.0-dev.1", SdkManifest } }, failDownload: true) } };
			Assert.AreEqual(0, (await UpdaterParametersExtension.LoadUnoSdkMappings(parameters, CancellationToken.None)).Count, "Download throws.");

			parameters = new UpdaterParameters { TargetVersions = { "dev" }, Feeds = { new SdkPackageFeed(new Dictionary<string, string> { { "7.1.0-dev.1", "not json" } }) } };
			Assert.AreEqual(0, (await UpdaterParametersExtension.LoadUnoSdkMappings(parameters, CancellationToken.None)).Count, "Broken packages.json.");
		}

		[TestMethod]
		public async Task GetLatestVersion_LoadsTheSdkMappingsFromTheFeeds_WhenNotSet()
		{
			var feed = new SdkPackageFeed(
				new Dictionary<string, string> { { "7.1.0-dev.200", SdkManifest } },
				otherPackages: new Dictionary<string, string[]> { { "Uno.New.Thing", new[] { "1.1.0-dev.3" } } });
			var parameters = new UpdaterParameters { TargetVersions = { "dev" }, Feeds = { feed } };

			var version = await parameters.GetLatestVersion(CancellationToken.None, new PackageReference("uno.new.thing", "1.0.0"));

			Assert.AreEqual(NuGetVersion.Parse("1.1.0-dev.3"), version?.Version);
			Assert.AreEqual(1, feed.Downloads, "The Uno.Sdk is downloaded once per run.");

			await parameters.GetLatestVersion(CancellationToken.None, new PackageReference("uno.new.thing", "1.0.0"));
			Assert.AreEqual(1, feed.Downloads, "The Uno.Sdk is downloaded once per run.");
		}

		/// <summary>
		/// Serves Uno.Sdk.Private versions as real .nupkg files holding the given packages.json.
		/// </summary>
		private sealed class SdkPackageFeed : IPackageFeed
		{
			private readonly Dictionary<string, string> _manifests;
			private readonly Dictionary<string, string[]> _otherPackages;
			private readonly bool _failDownload;

			public SdkPackageFeed(Dictionary<string, string> manifestsByVersion, bool failDownload = false, Dictionary<string, string[]> otherPackages = null)
			{
				_manifests = manifestsByVersion;
				_failDownload = failDownload;
				_otherPackages = otherPackages ?? new Dictionary<string, string[]>();
			}

			public int Downloads { get; private set; }

			public Uri Url => Constants.TestFeedUri;

			public bool IsPrivate => true;

			public Task<FeedVersion[]> GetPackageVersions(CancellationToken ct, PackageReference reference, string author = null)
			{
				var versions = reference.Identity.Id == UnoSdkPropertyMappings.SdkPackageId
					? _manifests.Keys.ToArray()
					: _otherPackages.GetValueOrDefault(reference.Identity.Id) ?? Array.Empty<string>();

				return Task.FromResult(versions.Select(v => new FeedVersion(v, Url)).ToArray());
			}

			public Task<LocalPackage> DownloadPackage(CancellationToken ct, PackageIdentity packageIdentity, string location)
			{
				Downloads++;

				if(_failDownload)
				{
					throw new InvalidOperationException("Feed unavailable.");
				}

				if(!_manifests.TryGetValue(packageIdentity.Version.ToNormalizedString(), out var manifest))
				{
					return Task.FromResult<LocalPackage>(null);
				}

				var path = Path.Join(location, $"{packageIdentity}.nupkg");
				using(var archive = ZipFile.Open(path, ZipArchiveMode.Create))
				{
					var entry = archive.CreateEntry(UnoSdkPropertyMappings.ManifestEntryPath);
					using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
					writer.Write(manifest);
				}

				return Task.FromResult(new LocalPackage(packageIdentity, path));
			}

			public Task<Dictionary<NuGetFramework, PackageDependency[]>> GetDependencies(CancellationToken ct, PackageIdentity packageIdentity)
				=> Task.FromResult(new Dictionary<NuGetFramework, PackageDependency[]>());

			public Task<bool> PushPackage(CancellationToken ct, LocalPackage package) => throw new NotSupportedException();
		}
	}
}
