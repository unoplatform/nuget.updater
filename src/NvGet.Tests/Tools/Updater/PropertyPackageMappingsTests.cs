using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvGet.Tools.Updater.Entities;

namespace NvGet.Tests.Tools.Updater
{
	[TestClass]
	public class PropertyPackageMappingsTests
	{
		[DataTestMethod]
		[DataRow("UnoExtensions", "Uno.Extensions.Core")]
		[DataRow("UnoMaterial", "Uno.Material.WinUI")]
		[DataRow("UnoThemes", "Uno.Themes.WinUI")]
		public void GivenMappedProperty_WhenTryGetFallbackMapping_ReturnsFallback(string property, string expectedFallbackPackageId)
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping(property, out var fallbackPackageId, out var reason);

			Assert.IsTrue(result);
			Assert.AreEqual(expectedFallbackPackageId, fallbackPackageId);
			Assert.IsTrue(reason.Contains("outdated"));
		}

		// UnoToolkit / UnoWinui / UnoResizetizer / UnoTemplates intentionally not mapped:
		// their derived lowercase IDs match real, current packages on NuGet.org.
		[DataTestMethod]
		[DataRow("UnknownProperty")]
		[DataRow("UnoToolkit")]
		[DataRow("UnoWinui")]
		[DataRow("UnoResizetizer")]
		[DataRow("UnoTemplates")]
		[DataRow(null)]
		public void GivenUnmappedProperty_WhenTryGetFallbackMapping_ReturnsFalse(string property)
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping(property, out var fallbackPackageId, out var reason);

			Assert.IsFalse(result);
			Assert.IsNull(fallbackPackageId);
			Assert.IsNull(reason);
		}
	}
}
