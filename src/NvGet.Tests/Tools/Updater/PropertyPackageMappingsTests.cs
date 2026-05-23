using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvGet.Tools.Updater.Entities;

namespace NvGet.Tests.Tools.Updater
{
	[TestClass]
	public class PropertyPackageMappingsTests
	{
		[TestMethod]
		public void GivenUnoExtensions_WhenTryGetFallbackMapping_ReturnsUnoExtensionsCore()
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnoExtensions", out var fallbackPackageId, out var reason);

			Assert.IsTrue(result);
			Assert.AreEqual("Uno.Extensions.Core", fallbackPackageId);
			Assert.IsTrue(reason.Contains("outdated"));
		}

		[TestMethod]
		public void GivenUnoMaterial_WhenTryGetFallbackMapping_ReturnsUnoMaterialWinUI()
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnoMaterial", out var fallbackPackageId, out var reason);

			Assert.IsTrue(result);
			Assert.AreEqual("Uno.Material.WinUI", fallbackPackageId);
			Assert.IsTrue(reason.Contains("outdated"));
		}

		[TestMethod]
		public void GivenUnoThemes_WhenTryGetFallbackMapping_ReturnsUnoThemesWinUI()
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnoThemes", out var fallbackPackageId, out var reason);

			Assert.IsTrue(result);
			Assert.AreEqual("Uno.Themes.WinUI", fallbackPackageId);
			Assert.IsTrue(reason.Contains("outdated"));
		}

		[TestMethod]
		public void GivenUnknownProperty_WhenTryGetFallbackMapping_ReturnsFalse()
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnknownProperty", out var fallbackPackageId, out var reason);

			Assert.IsFalse(result);
			Assert.IsNull(fallbackPackageId);
			Assert.IsNull(reason);
		}

		[TestMethod]
		public void GivenUnoToolkit_WhenTryGetFallbackMapping_ReturnsFalse()
		{
			// UnoToolkit / UnoWinui / UnoResizetizer / UnoTemplates intentionally not mapped:
			// their derived lowercase IDs match real, current packages on NuGet.org.
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnoToolkit", out var fallbackPackageId, out var reason);

			Assert.IsFalse(result);
			Assert.IsNull(fallbackPackageId);
			Assert.IsNull(reason);
		}

		[TestMethod]
		public void GivenNull_WhenTryGetFallbackMapping_ReturnsFalse()
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping(null, out var fallbackPackageId, out var reason);

			Assert.IsFalse(result);
			Assert.IsNull(fallbackPackageId);
			Assert.IsNull(reason);
		}
	}
}
