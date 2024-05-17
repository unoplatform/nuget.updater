﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NvGet.Entities;
using Uno.Extensions;

namespace NvGet.Extensions
{
	public static class FeedVersionExtensions
	{
		public static bool IsMatchingVersion(
			this FeedVersion version,
			string tag,
			bool isStrict
		)
		{
			var releaseLabels = version?.Version?.ReleaseLabels;

			if(tag.IsNullOrEmpty() || tag == "stable")
			{
				return releaseLabels?.None() ?? true; //Stable versions have no release labels
			}

			var hasTag = ContainsTag(releaseLabels, tag);

			var isMatch = isStrict
				? releaseLabels?.Count() == 2 && hasTag // Check strictly for packages with versions "dev.XXXX"
				: hasTag; // Allow packages with versions "dev.XXXX.XXXX"

			if(isMatch)
			{
				return true;
			}

			// Search for multipart tags, such as `feature.test`
			var tagDotCount = tag.Count(c => c == '.') + 1;

			if (!isMatch && tagDotCount > 1)
			{
				var multiPart = string.Join(".", releaseLabels.Take(tagDotCount));

				return ContainsTag(new[] { multiPart }, tag);
			}

			return false;
		}

		private static bool ContainsTag(IEnumerable<string> releaseLabels, string tag)
			=> tag.HasValue()
				&& (releaseLabels?.Any(label => Regex.IsMatch(label, tag, RegexOptions.IgnoreCase)) ?? false);
	}
}
