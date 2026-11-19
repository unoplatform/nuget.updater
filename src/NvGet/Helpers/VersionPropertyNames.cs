using System;
using System.Linq;
using Uno.Extensions;

namespace NvGet.Helpers
{
	/// <summary>
	/// Turns an MSBuild version property name into the package ID the updater looks up.
	/// </summary>
	public static class VersionPropertyNames
	{
		/// <summary>
		/// Strips the trailing "Version" and splits on capitals that follow a lowercase letter,
		/// then lowercases: <c>UnoThemesVersion</c> → <c>uno.themes</c>,
		/// <c>UnoCSharpMarkupVersion</c> → <c>uno.csharp.markup</c>.
		/// </summary>
		/// <remarks>
		/// This is the name-based guess used for every <c>*Version</c> property found in a
		/// <c>PropertyGroup</c>. When the guessed package doesn't exist, the updater falls back to
		/// <c>PropertyPackageMappings</c> and to the Uno.Sdk's own manifest
		/// (<c>UnoSdkPropertyMappings</c>), both keyed by the ID this method produces.
		/// </remarks>
		public static string ToPackageId(string versionPropertyName)
		{
			if(string.IsNullOrEmpty(versionPropertyName))
			{
				return versionPropertyName;
			}

			var trimmedName = versionPropertyName.TrimEnd("Version");

			var nameParts = trimmedName
				.Select((c, i) =>
					i > 0
					&& char.IsUpper(c)
					&& !char.IsUpper(trimmedName[i - 1])
					? "." + c
					: c.ToString());

			return string.Concat(nameParts).ToLowerInvariant();
		}
	}
}
