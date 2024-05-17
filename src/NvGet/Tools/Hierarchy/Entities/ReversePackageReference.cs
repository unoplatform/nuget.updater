﻿using System.Collections.Generic;
using NuGet.Packaging.Core;

namespace NvGet.Tools.Hierarchy.Entities
{
	public class ReversePackageReference
	{
		public PackageIdentity Identity { get; set; }

		public List<PackageIdentity> ReferencedBy { get; set; }
	}
}
