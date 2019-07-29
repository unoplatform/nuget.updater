﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mono.Options;
using NuGet.Updater.Entities;

namespace NuGet.Updater.Tool
{
	public class Program
	{
		private static bool _isParameterSet = default;
		private static UpdaterParameters _parameters = default;

		public static async Task Main(string[] args)
		{
			var isHelp = false;
			var isSilent = false;
			string summaryFile = default;

			var options = new OptionSet
			{
				{ "help|h", "Displays this help screen", s => isHelp = true },
				{ "solution=|s=", "The path to the solution to update", s => Set(p => p.SolutionRoot = s) },
				{ "feed=|f=", "A private feed to use for the update; the format is {url}|{accessToken}; can be specified multiple times", s => AddPrivateFeed(s) },
				{ "version=|versions=|v=", "The target versions to use", s => Set(p => p.TargetVersions = GetList(s))},
				{ "useNuGetorg|n", "Whether to pull packages from NuGet.org", _ => Set(p => p.IncludeNuGetOrg = true )},
				{ "packagesOwner=|o=", "The owner of the packages to update; must be specified if useNuGetorg is true", s => Set(p => p.PackagesOwner = s)},
				{ "allowDowngrade=|d=", "Whether package downgrade is allowed", s => Set(p => p.IsDowngradeAllowed = GetBoolean(s))},
				{ "ignore=|i=", "A comma-separated list of packages to ignore", s => Set(p => p.PackagesToIgnore = GetList(s)) },
				{ "update=|u=", "A comma-separated list of packages to update; not specifying this will update all packages found", s => Set(p => p.PackagesToUpdate = GetList(s)) },
				{ "outputFile=|of=", "The path to a file where the update summary will be written", s => summaryFile = s },
				{ "silent", "Suppress all output from NuGet Updater", _ => isSilent = true },
			};

			_isParameterSet = false;
			_parameters = new UpdaterParameters
			{
				UpdateTarget = UpdateTarget.All,
				PrivateFeeds = new Dictionary<string, string>(),
			};

			options.Parse(args);

			if(isHelp || !_isParameterSet)
			{
				Console.WriteLine("NuGet Updater is a tool allowing the automatic update of the NuGet packages found in a solution");
				options.WriteOptionDescriptions(Console.Out);
			}
			else
			{
				await NuGetUpdater.UpdateAsync(CancellationToken.None, _parameters, isSilent ? null : Console.Out, summaryFile);
			}
		}

		private static void Set(Action<UpdaterParameters> set)
		{
			set(_parameters);
			_isParameterSet = true;
		}

		private static void AddPrivateFeed(string value)
		{
			const char separator = '|';
			if(value.Contains(separator))
			{
				var parts = value.Split(separator);
				_parameters.PrivateFeeds.Add(parts[0], parts[1]);
			}
			else
			{
				_parameters.PrivateFeeds.Add(value, null);
			}

			_isParameterSet = true;
		}

		private static bool GetBoolean(string value, bool fallbackValue = false)
		{
			if(bool.TryParse(value, out var boolean))
			{
				return boolean;
			}

			return fallbackValue;
		}

		private static List<string> GetList(string value)
		{
			var list = new List<string>();

			if(value.Contains(","))
			{
				list.AddRange(value.Split(","));
			}
			else
			{
				list.Add(value);
			}

			return list;
		}
	}
}