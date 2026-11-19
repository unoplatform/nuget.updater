using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NvGet.Contracts;
using NvGet.Entities;
using NuGet.Versioning;

namespace NvGet.Tools.Updater.Entities
{
	public class UpdaterParameters
	{
		/// <summary>
		/// Gets or sets the location of the solution to update.
		/// </summary>
		public string SolutionRoot { get; set; }

		/// <summary>
		/// Gets a list of feeds to get packages from.
		/// </summary>
		public ICollection<IPackageFeed> Feeds { get; } = new List<IPackageFeed>();

		/// <summary>
		/// Gets the versions to update to (stable, dev, beta, etc.), in order of priority.
		/// </summary>
		public ICollection<string> TargetVersions { get; } = new List<string>();

		/// <summary>
		/// Gets the paths to ignore when searching for updatable files.
		/// </summary>
		public ICollection<string> ExcludedPaths { get; } = new List<string>();

		/// <summary>
		/// Gets or sets a value indicating whether the version should exactly match the target version.
		/// </summary>
		public bool Strict { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether whether the packages can be downgraded if the version found is lower than the current one.
		/// </summary>
		public bool IsDowngradeAllowed { get; set; }

		/// <summary>
		/// Gets or sets the type of files to update.
		/// </summary>
		public FileType UpdateTarget { get; set; }

		/// <summary>
		/// Gets a list of packages to ignore.
		/// </summary>
		public ICollection<string> PackagesToIgnore { get; } = new List<string>();

		/// <summary>
		/// Gets a list of packages to update; all packages found will be updated if nothing is specified.
		/// </summary>
		public ICollection<string> PackagesToUpdate { get; } = new List<string>();

		/// <summary>
		/// Gets or sets the name of the authors of the packages to update; used with NuGet.org; packages from private feeds are assumed to be required. Multiple authors can be provided with a comma separated list.
		/// </summary>
		public string PackageAuthors { get; set; }

		/// <summary>
		/// Gets the version range overrides for specific packages.
		/// </summary>
		public IDictionary<string, (bool forceVersion, UpgradePolicy upgradePolicy, VersionRange range)> VersionOverrides { get; } = new Dictionary<string, (bool, UpgradePolicy, VersionRange)>();

		/// <summary>
		/// Gets the csproj properties that should be updated for corresponding package.
		/// </summary>
		public ICollection<(string PropertyName, string PackageId)> UpdateProperties { get; } = new List<(string PropertyNaem, string PackageId)>();

		/// <summary>
		/// Gets or sets a value indicating whether to actually write the updates to the files.
		/// </summary>
		public bool IsDryRun { get; set; }

		/// <summary>
		/// Version property → package mappings published by the Uno.Sdk (see <see cref="UnoSdkPropertyMappings"/>).
		/// Leave unset to read them from the latest Uno.Sdk.Private on <see cref="Feeds"/> the first time a
		/// property can't be resolved by name; set it to use a fixed table (tests, offline runs).
		/// </summary>
		public UnoSdkPropertyMappings UnoSdkMappings
		{
			set => _unoSdkMappings = new Lazy<Task<UnoSdkPropertyMappings>>(Task.FromResult(value ?? UnoSdkPropertyMappings.Empty));
		}

		private Lazy<Task<UnoSdkPropertyMappings>> _unoSdkMappings;

		/// <summary>
		/// Gets the Uno.Sdk mappings, loading them once per run with <paramref name="loader"/>.
		/// </summary>
		internal Task<UnoSdkPropertyMappings> GetUnoSdkMappings(Func<Task<UnoSdkPropertyMappings>> loader)
			=> LazyInitializer.EnsureInitialized(ref _unoSdkMappings, () => new Lazy<Task<UnoSdkPropertyMappings>>(loader)).Value;
	}
}
