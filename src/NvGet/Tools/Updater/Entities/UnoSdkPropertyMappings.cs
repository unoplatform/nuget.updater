using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using NvGet.Helpers;

namespace NvGet.Tools.Updater.Entities
{
	/// <summary>
	/// Version property → package mappings read from the Uno.Sdk itself.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each group in the Uno.Sdk's <c>packages.json</c> names the MSBuild property that overrides its
	/// version (<c>"versionProperty": "UnoCSharpMarkupVersion"</c> on the CSharpMarkup group, added in
	/// unoplatform/uno#26611). A project that sets such a property gets a reference whose name-derived ID
	/// (<c>uno.csharp.markup</c>) doesn't exist on any feed; this table gives the package to look up
	/// instead (<c>Uno.WinUI.Markup</c>, the first package of the group). New Uno.Sdk properties are
	/// picked up without an updater change.
	/// </para>
	/// <para>
	/// Only groups whose packages are Uno packages (ID starting with <c>Uno.</c>) are kept: third-party
	/// groups (SkiaSharp, Microsoft.WindowsAppSDK, AndroidX…) follow the Uno.Sdk and are not floated by
	/// the canaries. Groups of an Uno.Sdk release ship together, so the first package stands for the group.
	/// </para>
	/// <para>
	/// Older Uno.Sdk versions don't have the field: the table is then empty and
	/// <see cref="PropertyPackageMappings"/> is the only fallback.
	/// </para>
	/// </remarks>
	public sealed class UnoSdkPropertyMappings
	{
		/// <summary>Package whose manifest is read. The public Uno.Sdk carries the same groups.</summary>
		public const string SdkPackageId = "Uno.Sdk.Private";

		/// <summary>Location of the manifest inside the Uno.Sdk package.</summary>
		public const string ManifestEntryPath = "targets/netstandard2.0/packages.json";

		public static UnoSdkPropertyMappings Empty { get; } = new UnoSdkPropertyMappings(new Dictionary<string, (string, string)>(), source: null);

		// Key: name-derived package ID of the property (uno.csharp.markup), case-insensitive.
		private readonly IReadOnlyDictionary<string, (string PackageId, string PropertyName)> _mappings;

		private UnoSdkPropertyMappings(IReadOnlyDictionary<string, (string, string)> mappings, string source)
		{
			_mappings = mappings;
			Source = source;
		}

		/// <summary>Where the mappings come from, for logs (for example "Uno.Sdk.Private 7.1.0-dev.500").</summary>
		public string Source { get; }

		public int Count => _mappings.Count;

		/// <summary>
		/// Looks up the package to use for a property-derived reference ID.
		/// </summary>
		/// <param name="derivedPackageId">The ID the property name produced (see <see cref="VersionPropertyNames.ToPackageId"/>).</param>
		public bool TryGetPackageId(string derivedPackageId, out string packageId, out string propertyName)
		{
			if(derivedPackageId != null && _mappings.TryGetValue(derivedPackageId, out var mapping))
			{
				(packageId, propertyName) = mapping;
				return true;
			}

			packageId = null;
			propertyName = null;
			return false;
		}

		/// <summary>
		/// Builds the mappings from the contents of a Uno.Sdk <c>packages.json</c>.
		/// </summary>
		public static UnoSdkPropertyMappings Parse(string packagesJson, string source)
		{
			var mappings = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

			using var document = JsonDocument.Parse(packagesJson.TrimStart('﻿'));

			foreach(var group in document.RootElement.EnumerateArray())
			{
				if(!group.TryGetProperty("versionProperty", out var propertyElement)
					|| propertyElement.ValueKind != JsonValueKind.String
					|| !group.TryGetProperty("packages", out var packagesElement)
					|| packagesElement.ValueKind != JsonValueKind.Array)
				{
					continue;
				}

				var propertyName = propertyElement.GetString();
				var packageId = packagesElement
					.EnumerateArray()
					.Where(p => p.ValueKind == JsonValueKind.String)
					.Select(p => p.GetString())
					.FirstOrDefault();

				if(string.IsNullOrWhiteSpace(propertyName)
					|| string.IsNullOrWhiteSpace(packageId)
					|| !packageId.StartsWith("Uno.", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				// A property can drive several groups (MauiVersion); the first one wins.
				var derivedId = VersionPropertyNames.ToPackageId(propertyName);
				if(!mappings.ContainsKey(derivedId))
				{
					mappings[derivedId] = (packageId, propertyName);
				}
			}

			return new UnoSdkPropertyMappings(mappings, source);
		}

		/// <summary>
		/// Reads the mappings from a downloaded Uno.Sdk package (.nupkg).
		/// </summary>
		public static UnoSdkPropertyMappings FromPackage(string nupkgPath, string source)
		{
			using var archive = ZipFile.OpenRead(nupkgPath);

			var entry = archive.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), ManifestEntryPath, StringComparison.OrdinalIgnoreCase));
			if(entry == null)
			{
				return Empty;
			}

			using var reader = new StreamReader(entry.Open());
			return Parse(reader.ReadToEnd(), source);
		}
	}
}
