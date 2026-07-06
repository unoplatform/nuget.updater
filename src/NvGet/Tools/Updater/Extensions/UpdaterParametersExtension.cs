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

			var result = await ResolveLatestVersion(parameters, ct, reference, authors, manualVersion);

			// Built-in safety net: when the property-name-derived package ID is a known outdated/deprecated
			// base package (e.g. uno.material → 5.x stale; modern consumers reference Uno.Material.WinUI),
			// also look up the canonical replacement and prefer it when it returns a newer version.
			//
			// Suspicion gate: only consider the fallback if the standard lookup looks broken — either it
			// returned nothing, or it returned a version *older* than the reference's current version
			// (a strong signal that the local file is tracking the modern variant via property indirection
			// while the heuristic-derived package ID is a frozen/legacy line). When the standard lookup
			// returns a newer version than the local reference, the heuristic-derived package is still
			// alive and we must not overwrite it with the fallback's version — that would write a version
			// that doesn't exist for the original package ID and break the restore.
			//
			// Users who need explicit, non-fallback property→package mappings should use --updateProperties=.
			var standardIsSuspicious = result == null || reference.Identity.Version > result.Version;
			if(standardIsSuspicious)
			{
				var propertyFragment = ConvertPackageIdToPropertyFragment(reference.Identity.Id);
				if(PropertyPackageMappings.TryGetFallbackMapping(propertyFragment, out var fallbackPackageId, out var reason))
				{
					var fallbackReference = new PackageReference(
						new PackageIdentity(fallbackPackageId, reference.Identity.Version),
						reference.Files
					);
					var fallbackResult = await ResolveLatestVersion(parameters, ct, fallbackReference, authors, manualVersion);

					if(fallbackResult != null && (result == null || fallbackResult.Version > result.Version))
					{
						PackageFeed.Logger.LogInformation($"Applied property fallback mapping: {reference.Identity.Id} -> {fallbackPackageId} ({reason})");
						result = fallbackResult;
					}
				}
			}

			return result;
		}

		private static async Task<FeedVersion> ResolveLatestVersion(
			UpdaterParameters parameters,
			CancellationToken ct,
			PackageReference reference,
			string[] authors,
			(bool forceVersion, UpgradePolicy upgradePolicy, VersionRange range) manualVersion)
		{
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

			return versionsPerTarget
				.Select(g => g.FirstOrDefault())
				.OrderByDescending(v => v.Version)
				.FirstOrDefault();
		}

		/// <summary>
		/// Best-effort title-casing of a dot-separated lowercase package ID into a property-name
		/// fragment, used as a lookup key against <c>PropertyPackageMappings.WellKnownMappings</c>.
		/// </summary>
		/// <remarks>
		/// This is NOT a true inverse of the property-name → package-id heuristic in
		/// <c>XmlDocumentExtensions.GetPackageReferences</c>: that heuristic lowercases everything,
		/// so acronym information is lost on the forward path. For example, both
		/// <c>UnoWinUIVersion</c> → <c>uno.win.ui</c> and <c>UnoWinuiVersion</c> → <c>uno.winui</c>
		/// are valid forward conversions, but this helper can only produce a single output per input
		/// (<c>UnoWinUi</c> and <c>UnoWinui</c> respectively). The well-known table is therefore keyed
		/// by the fragment this helper actually produces; entries with acronyms must match accordingly.
		/// All current entries (<c>UnoMaterial</c>, <c>UnoThemes</c>, <c>UnoExtensions</c>) avoid
		/// the ambiguity.
		/// </remarks>
		internal static string ConvertPackageIdToPropertyFragment(string packageId)
		{
			if(string.IsNullOrEmpty(packageId))
			{
				return packageId;
			}

			var parts = packageId.Split('.', StringSplitOptions.RemoveEmptyEntries);
			return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
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
