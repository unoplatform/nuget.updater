using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NvGet.Contracts;
using NvGet.Entities;
using NvGet.Extensions;
using NuGet.Versioning;
using Uno.Extensions;
using Newtonsoft.Json;

namespace NvGet.Helpers
{
	public class GlobalJson
	{
		[JsonProperty("msbuild-sdks")]
		public Dictionary<string, string> MSBuildSdks { get; set; }
	}
}
