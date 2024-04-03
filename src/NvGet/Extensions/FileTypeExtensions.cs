using System.Linq;
using NvGet.Contracts;
using NvGet.Entities;

namespace NvGet.Extensions
{
	public static class FileTypeExtensions
	{
		public static string GetDescription(this FileType target)
		{
			return target switch
			{
				FileType.Nuspec => ".nuspec",
				FileType.Csproj => ".csproj",
				FileType.DirectoryProps => "Directory.Build.targets",
				FileType.DirectoryTargets => "Directory.Build.props",
				FileType.CentralPackageManagement => "Directory.Packages.props",
				FileType.GlobalJson => "global.json",
				_ => default,
			};
		}

		public static bool HasAnyFlag(this FileType target, params FileType[] others)
		{
			return others.Any(t => t.HasFlag(target));
		}
	}
}
