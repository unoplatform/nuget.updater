using System.Collections.Generic;

namespace NvGet.Tools.Updater.Entities
{
	/// <summary>
	/// Provides well-known fallback mappings for MSBuild properties that reference outdated or deprecated base packages.
	/// When a property (e.g., UnoExtensionsVersion) references a package with no matching versions,
	/// these mappings provide alternative packages to check (e.g., Uno.Extensions.Core).
	/// </summary>
	public static class PropertyPackageMappings
	{
		/// <summary>
		/// Well-known property-to-package fallback mappings for Uno Platform packages.
		/// Key: Property name suffix (e.g., "UnoExtensions" from "UnoExtensionsVersion")
		/// Value: (FallbackPackageId, Reason)
		/// </summary>
		public static readonly IReadOnlyDictionary<string, (string FallbackPackageId, string Reason)> WellKnownMappings =
			new Dictionary<string, (string, string)>
			{
				// Uno.Extensions base package is outdated (v2.4.2 from 2023) on NuGet.org
				// All active sub-packages like Uno.Extensions.Core, Uno.Extensions.Navigation, etc. have current versions
				["UnoExtensions"] = ("Uno.Extensions.Core", "Base package Uno.Extensions is outdated; using Uno.Extensions.Core for version resolution"),
				
				// Uno.Material base package is outdated (v5.7.3 from 4 months ago) vs Uno.Material.WinUI (v6.0.2, 1 month ago)
				["UnoMaterial"] = ("Uno.Material.WinUI", "Base package Uno.Material is outdated; using Uno.Material.WinUI for version resolution"),
				
				// Add other known mappings as needed
				// ["UnoToolkit"] = ("Uno.Toolkit.WinUI", "Reason for fallback"),
			};

		/// <summary>
		/// Gets the fallback package ID and reason for a given property name.
		/// </summary>
		/// <param name="propertyNameWithoutVersion">The property name without the "Version" suffix (e.g., "UnoExtensions")</param>
		/// <param name="fallbackPackageId">Output: The package ID to use as fallback</param>
		/// <param name="reason">Output: The reason for the fallback</param>
		/// <returns>True if a fallback mapping exists; otherwise false</returns>
		public static bool TryGetFallbackMapping(
			string propertyNameWithoutVersion,
			out string fallbackPackageId,
			out string reason)
		{
			if (WellKnownMappings.TryGetValue(propertyNameWithoutVersion, out var mapping))
			{
				fallbackPackageId = mapping.FallbackPackageId;
				reason = mapping.Reason;
				return true;
			}

			fallbackPackageId = null;
			reason = null;
			return false;
		}
	}
}
