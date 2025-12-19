using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using NvGet.Contracts;
using NvGet.Entities;
using NvGet.Extensions;
using NvGet.Helpers;
using NvGet.Tools.Updater.Entities;
using Uno.Extensions;

namespace NvGet.Tools.Updater.Extensions
{
	public static class UpdaterParametersExtension
	{
		internal static IEnumerable<string> GetSummary(this UpdaterParameters parameters)
		{
			yield return $"## Configuration";

			yield return $"- Targeting solution {MarkdownHelper.CodeBlock(parameters.SolutionRoot)}";

			var files = parameters.UpdateTarget == FileType.All
				? Enum
					.GetValues(typeof(FileType))
					.Cast<FileType>()
					.Select(t => t.GetDescription())
					.Trim()
				: new[] { parameters.UpdateTarget.GetDescription() };

			yield return $"- Updating files of type {MarkdownHelper.CodeBlocksEnumeration(files)}";

			if(parameters.Feeds?.Any() ?? false)
			{
				yield return $"- Fetching packages from {MarkdownHelper.CodeBlocksEnumeration(parameters.Feeds.Select(s => s.Url.OriginalString))}";
			}

			if(parameters.PackageAuthors.HasValue())
			{
				yield return $"- Limiting to public packages authored by {MarkdownHelper.Bold(parameters.PackageAuthors)}";
			}

			yield return $"- Using {MarkdownHelper.CodeBlocksEnumeration(parameters.TargetVersions)} versions {(parameters.Strict ? "(exact match)" : "")}";

			if(parameters.IsDowngradeAllowed)
			{
				yield return $"- Downgrading packages if a lower version is found";
			}

			if(parameters.PackagesToUpdate?.Any() ?? false)
			{
				yield return $"- Updating only {MarkdownHelper.CodeBlocksEnumeration(parameters.PackagesToUpdate)}";
			}
		}

		public static UpdaterParameters Validate(this UpdaterParameters parameters)
		{
			if(parameters.SolutionRoot.IsNullOrEmpty())
			{
				throw new InvalidOperationException("The solution root must be specified");
			}

			return parameters;
		}

		/// <summary>
		/// Gets the latest version for the given reference by looking up first in a list of known packages.
		/// Useful in the cases where refernces to multiple versions of the same packages are found.
		/// </summary>
		public static async Task<FeedVersion> GetLatestVersion(
			this UpdaterParameters parameters,
			CancellationToken ct,
			IEnumerable<UpdaterPackage> knownPackages,
			PackageReference reference
		)
		{
			var knownVersion = knownPackages.FirstOrDefault(p => p.PackageId == reference.Identity.Id)?.Version;

			if(knownVersion == null)
			{
				knownVersion = await parameters.GetLatestVersion(ct, reference);
			}

			return knownVersion;
		}

		public static async Task<FeedVersion> GetLatestVersion(
			this UpdaterParameters parameters,
			CancellationToken ct,
			PackageReference reference
		)
		{
			if(parameters.VersionOverrides.TryGetValue(reference.Identity.Id, out var manualVersion) && manualVersion.forceVersion)
			{
				PackageFeed.Logger.LogInformation($"Overriding version for {reference.Identity.Id}");
				return new FeedVersion(manualVersion.range.MinVersion);
			}

			var authors = parameters.PackageAuthors?.Split(',') is { Length: > 0 } value
				? value
				: new string[] { null }; // Invoke GetPackageVersions with a null parameter

			var availableVersions = await Task.WhenAll(parameters
				.Feeds
				.SelectMany(f => authors.Select(author => f.GetPackageVersions(ct, reference, author)))
			);

			var versionsPerTarget = availableVersions
				.SelectMany(x => x)
				.Where(v => manualVersion
					.range?.Satisfies(v.Version) ?? true)
				.Where(v => IsUpgradable(manualVersion.upgradePolicy, reference, v.Version))
				.OrderByDescending(v => v)
				.GroupBy(version => parameters
					.TargetVersions
					.FirstOrDefault(
						t => version.IsMatchingVersion(t, parameters.Strict)))
				.Where(g => g.Key.HasValue());

			var result = versionsPerTarget
				.Select(g => g.FirstOrDefault())
				.OrderByDescending(v => v.Version)
				.FirstOrDefault();

			// Check for fallback package mappings if configured for this package
			// (applies even when a version was found, as the base package may be outdated)
			var fallbackResult = await TryGetFallbackVersion(parameters, ct, reference, authors, manualVersion);
			
			// Use fallback result if it's newer than the base package result
			if (fallbackResult != null && (result == null || fallbackResult.Version > result.Version))
			{
				result = fallbackResult;
			}

			return result;
		}

		private static async Task<FeedVersion> TryGetFallbackVersion(
			UpdaterParameters parameters,
			CancellationToken ct,
			PackageReference reference,
			string[] authors,
			(bool forceVersion, UpgradePolicy upgradePolicy, VersionRange range) manualVersion)
		{
			// Check user-provided custom mappings first
			if (parameters.PropertyPackageFallbackMappings.TryGetValue(reference.Identity.Id, out var customFallbackId))
			{
				PackageFeed.Logger.LogInformation($"Using custom fallback mapping: {reference.Identity.Id} -> {customFallbackId}");
				return await GetVersionForFallbackPackage(parameters, ct, reference, customFallbackId, authors, manualVersion);
			}

			// Check well-known mappings
			// Convert package ID back to property name format (e.g., "uno.extensions" -> "UnoExtensions")
			var propertyName = ConvertPackageIdToPropertyName(reference.Identity.Id);
			
			if (NvGet.Tools.Updater.Entities.PropertyPackageMappings.TryGetFallbackMapping(
				propertyName, out var fallbackPackageId, out var reason))
			{
				PackageFeed.Logger.LogInformation($"Property fallback mapping: {reference.Identity.Id} -> {fallbackPackageId}. {reason}");
				return await GetVersionForFallbackPackage(parameters, ct, reference, fallbackPackageId, authors, manualVersion);
			}

			return null;
		}

		private static async Task<FeedVersion> GetVersionForFallbackPackage(
			UpdaterParameters parameters,
			CancellationToken ct,
			PackageReference reference,
			string fallbackPackageId,
			string[] authors,
			(bool forceVersion, UpgradePolicy upgradePolicy, VersionRange range) manualVersion)
		{
			// Create a new reference with the fallback package ID
			var fallbackReference = new PackageReference(
				new PackageIdentity(fallbackPackageId, reference.Identity.Version),
				reference.Files
			);

			var fallbackVersions = await Task.WhenAll(parameters
				.Feeds
				.SelectMany(f => authors.Select(author => f.GetPackageVersions(ct, fallbackReference, author)))
			);

			var fallbackVersionsPerTarget = fallbackVersions
				.SelectMany(x => x)
				.Where(v => manualVersion.range?.Satisfies(v.Version) ?? true)
				.Where(v => IsUpgradable(manualVersion.upgradePolicy, fallbackReference, v.Version))
				.OrderByDescending(v => v)
				.GroupBy(version => parameters
					.TargetVersions
					.FirstOrDefault(t => version.IsMatchingVersion(t, parameters.Strict)))
				.Where(g => g.Key.HasValue());

			return fallbackVersionsPerTarget
				.Select(g => g.FirstOrDefault())
				.OrderByDescending(v => v.Version)
				.FirstOrDefault();
		}

		private static string ConvertPackageIdToPropertyName(string packageId)
		{
			// Convert "uno.extensions" -> "UnoExtensions"
			// Split by dots and capitalize each part
			var parts = packageId.Split('.');
			var propertyName = string.Join("", parts.Select(p => 
				char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1) : "")));
			return propertyName;
		}

		private static bool IsUpgradable(
			UpgradePolicy upgradePolicy
			, PackageReference reference
			, NuGetVersion version)
		{
			if (upgradePolicy == UpgradePolicy.Major)
			{
				return true;
			}
			else if (upgradePolicy == UpgradePolicy.Minor)
			{
				return version.Version < new Version(reference.Identity.Version.Major + 1, 0, 0);
			}
			else if (upgradePolicy == UpgradePolicy.Patch)
			{
				return version.Version < new Version(reference.Identity.Version.Major, reference.Identity.Version.Minor + 1, 0);
			}
			else
			{
				throw new NotSupportedException($"Upgrade policy {upgradePolicy} is not supported");
			}
		}
	}
}
