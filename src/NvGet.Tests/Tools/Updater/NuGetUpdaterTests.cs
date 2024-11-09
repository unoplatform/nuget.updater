using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvGet.Contracts;
using NvGet.Tools.Tests.Entities;
using NvGet.Tools.Updater;
using NvGet.Tools.Updater.Entities;
using NvGet.Tools.Updater.Log;
using Uno.Extensions;

namespace NvGet.Tests.Tools
{
	[TestClass]
	public class NuGetUpdaterTests
	{
		private static readonly Dictionary<string, string[]> TestPackages = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
		{
			{"Uno.UI", new[] { "1.0", "1.1-dev.1" } },
			{"Uno.Core", new[] { "1.0", "1.0-beta.1" } },
			{"unoplatform.NuGet.Updater", new[] { "1.0-beta.1" } },
		};

		private static readonly TestPackageFeed TestFeed = new TestPackageFeed(new Uri("http://localhost"), TestPackages);

		[TestMethod]
		public async Task GivenUnspecifiedTarget_NoUpdateIsMade()
		{
			var parameters = new UpdaterParameters
			{
				SolutionRoot = "MySolution.sln",
				UpdateTarget = FileType.Unspecified,
				TargetVersions = { "stable" },
				Feeds = { TestFeed },
			};

			var logger = new UpdaterLogger(Console.Out);

			var updater = new NuGetUpdater(parameters, logger);

			await updater.UpdatePackages(CancellationToken.None);

			Assert.IsTrue(logger.GetUpdates().None());
		}

		[TestMethod]
		[DataRow("stable", "uno.ui", "1.0")]
		[DataRow("dev", "uno.ui", "1.1-dev.1")]
		public async Task Given_VersionPropertyUpdate(string targetVersion, string package, string version)
		{
			var testTempPath = CreateTempWorkspace(".\\Scenarios\\Given_VersionPropertyUpdate");

			var parameters = new UpdaterParameters
			{
				SolutionRoot = testTempPath,
				UpdateTarget = FileType.All,
				TargetVersions = { targetVersion },
				Feeds = { TestFeed },
			};

			var logger = new UpdaterLogger(Console.Out);

			var updater = new NuGetUpdater(parameters, logger);

			await updater.UpdatePackages(CancellationToken.None);

			var updates = logger.GetUpdates();
			Assert.AreEqual(1, updates.Count());
			Assert.AreEqual(package, updates.First().PackageId.ToLowerInvariant());
			Assert.AreEqual(version, updates.First().UpdatedVersion.OriginalVersion);
		}

		private static string CreateTempWorkspace(string sourceFolder)
		{
			var tempFolder = Path.GetTempPath();
			var testTempPath = Path.Combine(tempFolder, Guid.NewGuid().ToString());
			Directory.CreateDirectory(testTempPath);
			foreach(var files in Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories))
			{
				File.Copy(files, Path.Combine(testTempPath, Path.GetFileName(files)));
			}

			return testTempPath;
		}
	}
}
