using System;
using System.Collections.Generic;

namespace NvGet.Tools.Updater.Entities
{
	/// <summary>
	/// Well-known fallback mappings for MSBuild version properties whose name-derived
	/// package ID points at an outdated/deprecated base package on NuGet.org.
	///
	/// Example: <c>&lt;UnoMaterialVersion&gt;</c> derives to <c>uno.material</c> (legacy UWP package),
	/// but the project actually consumes <c>Uno.Material.WinUI</c>. Without a fallback the updater
	/// silently skips the property because the derived base package's latest version is older
	/// than the property's current value.
	/// </summary>
	/// <remarks>
	/// User-supplied explicit property→package mappings can be provided via the existing
	/// <c>--updateProperties=</c> CLI argument (or the <c>useUpdateProperties</c> +
	/// <c>updatePropertiesFile</c> ADO task inputs). This well-known list is a built-in
	/// safety net for the common cases that every canary repo would otherwise need to configure.
	/// </remarks>
	public static class PropertyPackageMappings
	{
		/// <summary>
		/// Well-known property-fragment → fallback package mappings for Uno Platform packages.
		/// Key: property name with the trailing "Version" stripped (e.g. "UnoMaterial" for "UnoMaterialVersion").
		/// Value: tuple of (canonical package ID to look up instead, human-readable reason for logs).
		/// </summary>
		/// <remarks>
		/// Kept private so the table is genuinely immutable from the outside; the only supported access
		/// path is <see cref="TryGetFallbackMapping(string, out string, out string)"/>. New entries should
		/// be added here (with a one-line comment + one test method) when a new property→package
		/// mismatch is observed in production canary build logs.
		/// </remarks>
		private static readonly IReadOnlyDictionary<string, (string FallbackPackageId, string Reason)> WellKnownMappings =
			new Dictionary<string, (string, string)>(StringComparer.InvariantCultureIgnoreCase)
			{
				// Legacy `uno.material` (UWP-era) is at v5.x and stale.
				// Modern consumers reference `Uno.Material.WinUI` (currently 7.x).
				["UnoMaterial"] = ("Uno.Material.WinUI", "Base package Uno.Material is outdated; resolving against Uno.Material.WinUI."),

				// Legacy `uno.themes` (UWP-era) is at v5.x and stale.
				// Modern consumers reference `Uno.Themes.WinUI` (and the related `Uno.Simple.WinUI`,
				// which ships in lockstep with Themes.WinUI); using Uno.Themes.WinUI as the lookup.
				["UnoThemes"] = ("Uno.Themes.WinUI", "Base package Uno.Themes is outdated; resolving against Uno.Themes.WinUI."),

				// Legacy umbrella `uno.extensions` is at v2.x.
				// Modern consumers reference Uno.Extensions.* family packages (currently 7.x);
				// the family ships in lockstep so Uno.Extensions.Core is a safe canonical lookup.
				["UnoExtensions"] = ("Uno.Extensions.Core", "Base package Uno.Extensions is outdated; resolving against Uno.Extensions.Core."),
			};

		/// <summary>
		/// Looks up a well-known fallback for a given property-fragment.
		/// </summary>
		/// <param name="propertyNameWithoutVersion">Property name with the "Version" suffix stripped (e.g. "UnoExtensions").</param>
		/// <param name="fallbackPackageId">The canonical package ID to look up instead, or <c>null</c> if no mapping exists.</param>
		/// <param name="reason">Human-readable reason for logs, or <c>null</c> if no mapping exists.</param>
		/// <returns><c>true</c> when a well-known mapping is registered for the given property fragment.</returns>
		public static bool TryGetFallbackMapping(
			string propertyNameWithoutVersion,
			out string fallbackPackageId,
			out string reason)
		{
			fallbackPackageId = null;
			reason = null;

			if (string.IsNullOrEmpty(propertyNameWithoutVersion))
			{
				return false;
			}

			if (WellKnownMappings.TryGetValue(propertyNameWithoutVersion, out var mapping))
			{
				fallbackPackageId = mapping.FallbackPackageId;
				reason = mapping.Reason;
				return true;
			}

			return false;
		}
	}
}
