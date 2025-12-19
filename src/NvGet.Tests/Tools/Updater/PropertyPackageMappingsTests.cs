using Microsoft.VisualStudio.TestTools.UnitTesting;
using NvGet.Tools.Updater.Entities;

namespace NvGet.Tests.Tools.Updater
{
	[TestClass]
	public class PropertyPackageMappingsTests
	{
		[TestMethod]
		public void GivenUnoExtensions_ReturnsFallbackMapping()
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnoExtensions", out var fallbackPackageId, out var reason);

			Assert.IsTrue(result);
			Assert.AreEqual("Uno.Extensions.Core", fallbackPackageId);
			Assert.IsTrue(reason.Contains("outdated"));
		}

		[TestMethod]
		public void GivenUnoMaterial_ReturnsFallbackMapping()
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnoMaterial", out var fallbackPackageId, out var reason);

			Assert.IsTrue(result);
			Assert.AreEqual("Uno.Material.WinUI", fallbackPackageId);
			Assert.IsTrue(reason.Contains("outdated"));
		}

		[TestMethod]
		public void GivenUnknownProperty_ReturnsFalse()
		{
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnknownProperty", out var fallbackPackageId, out var reason);

			Assert.IsFalse(result);
			Assert.IsNull(fallbackPackageId);
			Assert.IsNull(reason);
		}

		[TestMethod]
		public void GivenUnoToolkit_ReturnsFalse()
		{
			// UnoToolkit is commented out in the mappings as it doesn't need fallback
			var result = PropertyPackageMappings.TryGetFallbackMapping("UnoToolkit", out var fallbackPackageId, out var reason);

			Assert.IsFalse(result);
			Assert.IsNull(fallbackPackageId);
			Assert.IsNull(reason);
		}
	}
}
