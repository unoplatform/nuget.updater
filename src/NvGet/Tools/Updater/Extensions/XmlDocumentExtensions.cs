using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;
using NvGet.Extensions;
using NvGet.Tools.Updater.Log;
using Uno.Extensions;
using Uno.Extensions.Specialized;


#if WINDOWS_UWP
using XmlDocument = Windows.Data.Xml.Dom.XmlDocument;
using XmlElement = Windows.Data.Xml.Dom.XmlElement;
#else
using XmlDocument = System.Xml.XmlDocument;
#endif

namespace NvGet.Tools.Updater.Extensions
{
	public static class XmlDocumentExtensions
	{
		/// <summary>
		/// Runs an <see cref="UpdateOperation"/> on Project Properties contained in a <see cref="XmlDocument"/>.
		/// </summary>
		/// <param name="document"></param>
		/// <param name="operation"></param>
		/// <returns></returns>
		public static IEnumerable<UpdateOperation> UpdateUpdateProperties(
			this XmlDocument document,
			UpdateOperation operation,
			ICollection<(string PropertyName, string PackageId)> updateProperties)
		{
			var operations = new List<UpdateOperation>();

			var packageId = operation.PackageId;

			foreach(var prop in updateProperties.Where(x=> x.PackageId == packageId))
			{
				var docProp = document.SelectElements(prop.PropertyName).FirstOrDefault();
				if(docProp is null)
				{
					continue;
				}

				var packageVersion = docProp.InnerText;
				if(packageVersion is { Length: > 0 })
				{
					var currentOperation = operation.WithPreviousVersion(packageVersion);

					if(currentOperation.ShouldProceed())
					{
						docProp.InnerText = currentOperation.UpdatedVersion.ToString();
					}

					operations.Add(currentOperation);
				}
			}

			return operations;
		}

		/// <summary>
		/// Runs an <see cref="UpdateOperation"/> on the PackageReferences contained in a <see cref="XmlDocument"/>.
		/// </summary>
		/// <param name="document"></param>
		/// <param name="operation"></param>
		/// <returns></returns>
		public static IEnumerable<UpdateOperation> UpdatePackageReferences(
			this XmlDocument document,
			UpdateOperation operation
		)
		{
			var operations = new List<UpdateOperation>();

			var packageId = operation.PackageId;

			var packageReferences = document.SelectElements("PackageReference", $"[@Include='{packageId}' or @Update='{packageId}']");
			var dotnetCliReferences = document.SelectElements("DotNetCliToolReference", $"[@Include='{packageId}' or @Update='{packageId}']");
			var packageVersions = document.SelectElements("PackageVersion", $"[@Include='{packageId}' or @Update='{packageId}']");

			foreach(var packageReference in packageReferences.Concat(dotnetCliReferences).Concat(packageVersions))
			{
				var packageVersion = packageReference.GetAttributeOrChild("Version");

				if(packageVersion.HasValue())
				{
					var currentOperation = operation.WithPreviousVersion(packageVersion);

					if(currentOperation.ShouldProceed())
					{
						packageReference.SetAttributeOrChild("Version", currentOperation.UpdatedVersion.ToString());
					}

					operations.Add(currentOperation);
				}
			}

			var propertyGroupVersionReferences = document
				.SelectElements("PropertyGroup")
				.SelectMany(pg => pg
					.SelectNodes("*")
					.OfType<XmlElement>()
					.Where(n => string.Equals(n.Name, packageId.Replace(".", string.Empty) + "Version", StringComparison.OrdinalIgnoreCase)));

			foreach(var versionProperty in propertyGroupVersionReferences)
			{
				if(NuGetVersion.TryParse(versionProperty.InnerText, out var previousVersion))
				{
					var currentOperation = operation.WithPreviousVersion(versionProperty.InnerText);

					if(currentOperation.ShouldProceed())
					{
						versionProperty.InnerText = currentOperation.UpdatedVersion.ToString();
					}

					operations.Add(currentOperation);
				}
			}

			return operations;
		}

		/// <summary>
		/// Gets the attribute or child (in this order) of the given <see cref="XmlElement"/> with the given name.
		/// </summary>
		private static string GetAttributeOrChild(this XmlElement element, string name)
		{
			var attribute = element.GetAttribute(name);

			if(attribute.HasValue())
			{
				return attribute;
			}

			return element.SelectNode(name)?.InnerText;
		}

		/// <summary>
		/// Sets the attribute or child (in this order) of the given <see cref="XmlElement"/> with the given name.
		/// </summary>
		private static void SetAttributeOrChild(this XmlElement element, string name, string value)
		{
			var attribute = element.GetAttribute(name);

			if(attribute.HasValue())
			{
				element.SetAttribute(name, value);
			}
			else
			{
				var node = element.SelectNode(name);

				if(node != null)
				{
					node.InnerText = value;
				}
			}
		}

		/// <summary>
		/// Runs an <see cref="UpdateOperation"/> on the dependencies contained in a <see cref="XmlDocument"/> loaded from a .nuspec file.
		/// </summary>
		/// <param name="document"></param>
		/// <param name="operation"></param>
		/// <returns></returns>
		public static IEnumerable<UpdateOperation> UpdateDependencies(
			this DocumentReference document,
			UpdateOperation operation
		)
		{
			if(document is XmlDocumentReference xmlDocReference)
			{
				var operations = new List<UpdateOperation>();

				foreach(var node in xmlDocReference.Document.SelectElements("dependency", $"[@id='{operation.PackageId}']"))
				{
					var versionNodeValue = node.GetAttribute("version");

					// only nodes with explicit version, skip expansion.
					if(!versionNodeValue.Contains("{", System.StringComparison.OrdinalIgnoreCase))
					{
						var currentOperation = operation.WithPreviousVersion(versionNodeValue);

						if(currentOperation.ShouldProceed())
						{
							node.SetAttribute("version", currentOperation.UpdatedVersion.ToString());
						}

						operations.Add(currentOperation);
					}
				}

				return operations;
			}
			else if(document is JsonDocumentReference jsonDocReference)
			{
				var operations = new List<UpdateOperation>();

				var globalJson = JObject.Parse(jsonDocReference.Contents);

				if(globalJson["msbuild-sdks"] is JObject msbuildSdks)
				{
					if(msbuildSdks.TryGetValue(operation.PackageId, out var value))
					{
						var currentOperation = operation.WithPreviousVersion(value.ToString());

						if(currentOperation.ShouldProceed())
						{
							// Use text replace to avoid changing the formatting of the file.

							var pattern = $"\"{operation.PackageId}\"\\s*:\\s*\"[^\"]*\"";
							var replacement = $"\"{operation.PackageId}\": \"{operation.UpdatedVersion}\"";
							jsonDocReference.Contents = Regex.Replace(jsonDocReference.Contents, pattern, replacement);
						}

						operations.Add(currentOperation);
					}
				}

				return operations;
			}
			else
			{
				throw new NotSupportedException();
			}
		}
	}
}
