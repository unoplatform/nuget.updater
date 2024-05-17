﻿using NuGet.Packaging.Core;

namespace NvGet.Extensions
{
	public static class PackageDependencyExtensions
	{
		public static PackageIdentity GetIdentity(this PackageDependency dependency) => new PackageIdentity(dependency.Id, dependency.VersionRange.MinVersion);
	}
}
