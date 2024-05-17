﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NvGet.Entities;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using Uno.Extensions;

#if WINDOWS_UWP
using System.Text.RegularExpressions;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using XmlDocument = Windows.Data.Xml.Dom.XmlDocument;
using XmlElement = Windows.Data.Xml.Dom.XmlElement;
using XmlNode = Windows.Data.Xml.Dom.IXmlNode;
#else
using XmlDocument = System.Xml.XmlDocument;
using XmlElement = System.Xml.XmlElement;
using XmlNode = System.Xml.XmlNode;
#endif

namespace NvGet.Extensions
{
	public static class XmlDocumentExtensions
	{
		/// <summary>
		/// Retrieves the UpdateProperties that need updating from the given XmlDocument.
		/// </summary>
		/// <param name="document"></param>
		/// <returns>A Dictionary where the key is the id of a package and the value its version.</returns>
		public static PackageIdentity[] GetUpdateProperties(this XmlDocument document, ICollection<(string PropertyName, string PackageId)> updateProperties)
		{
			var references = new List<PackageIdentity>();

			foreach(var prop in updateProperties)
			{
				var docProp = document.SelectElements(prop.PropertyName).FirstOrDefault();
				if(docProp is null)
				{
					continue;
				}

				var docPropIdentity = CreatePackageIdentity(prop.PackageId, docProp.InnerText);
				if(docPropIdentity is not null)
				{
					references.Add(docPropIdentity);
				}
			}

			return references
				.Trim()
				.ToArray();
		}

		/// <summary>
		/// Retrieves the PackageReferences from the given XmlDocument. If a package is present multiple time, only the first version will be returned.
		/// </summary>
		/// <param name="document"></param>
		/// <returns>A Dictionary where the key is the id of a package and the value its version.</returns>
		public static PackageIdentity[] GetPackageReferences(this XmlDocument document)
		{
			var references = new List<PackageIdentity>();

			var packageReferences = document.SelectElements("PackageReference");
			var dotnetCliReferences = document.SelectElements("DotNetCliToolReference");
			var packageVersionReferences = document.SelectElements("PackageVersion");

			foreach(var packageReference in packageReferences.Concat(dotnetCliReferences).Concat(packageVersionReferences))
			{
				var packageId = new[] { "Include", "Update", "Remove" }
					.Select(packageReference.GetAttribute)
					.FirstOrDefault(x => !string.IsNullOrEmpty(x));
				var packageVersion = packageReference.GetAttribute("Version");

				if(packageVersion.HasValue())
				{
					references.Add(CreatePackageIdentity(packageId, packageReference.GetAttribute("Version")));
				}
				else
				{
					var node = packageReference.SelectNode("Version");
					if(node != null)
					{
						references.Add(CreatePackageIdentity(packageId, node.InnerText));
					}
				}
			}

			// find all nodes inside a PropertyGroup node that for which the name is ending by Version
			var propertyGroupVersionReferences = document
				.SelectElements("PropertyGroup")
				.SelectMany(pg => pg.SelectNodes("*").OfType<XmlElement>())
				.Where(e => e.LocalName.EndsWith("Version", StringComparison.OrdinalIgnoreCase));

			foreach(var versionProperty in propertyGroupVersionReferences)
			{
				var originalTrimmedName = versionProperty
					.LocalName
					.TrimEnd("Version");

				var nameParts =
					originalTrimmedName
					.Select((c, i) =>
						i > 0
						&& char.IsUpper(c)
						&& !char.IsUpper(originalTrimmedName[i - 1])
						? "." + c
						: c.ToString());

				var packageName = string.Concat(nameParts).ToLowerInvariant();

				if(NuGetVersion.TryParse(versionProperty.InnerText, out var nugetVersion))
				{
					references.Add(CreatePackageIdentity(packageName, versionProperty.InnerText));
				}
			}

			return references
				.Trim()
				.ToArray();
		}

		/// <summary>
		/// Retrieves the dependency elements from the given XmlDocument. If a package is present multiple time, only the first version will be returned.
		/// </summary>
		/// <param name="document"></param>
		/// <returns>A Dictionary where the key is the id of a package and the value its version.</returns>
		public static PackageIdentity[] GetDependencies(this XmlDocument document)
			=> document
				.SelectElements("dependency")
				.Select(e => CreatePackageIdentity(e.GetAttribute("id"), e.GetAttribute("version")))
				.Trim()
				.ToArray();

		private static PackageIdentity CreatePackageIdentity(string id, string version)
		{
			if(NuGetVersion.TryParse(version, out var parsedVersion))
			{
				return new PackageIdentity(id, parsedVersion);
			}

			return default;
		}

		/// <summary>
		/// Opens the XML files where package references were found.
		/// </summary>
		/// <param name="references"></param>
		/// <param name="ct"></param>
		/// <returns></returns>
		public static async Task<Dictionary<string, DocumentReference>> OpenFiles(
			this IEnumerable<PackageReference> references,
			CancellationToken ct
		)
		{
			var files = references
				.SelectMany(r => r.Files)
				.SelectMany(g => g.Value.Select(file => (fileType: g.Key, file: file)))
				.Distinct();

			var documents = new Dictionary<string, DocumentReference>();

			foreach(var file in files)
			{
				if(file.fileType == Contracts.FileType.GlobalJson)
				{
					documents.Add(file.file, new JsonDocumentReference(System.IO.File.ReadAllText(file.file)));
				}
				else
				{
					documents.Add(file.file, new XmlDocumentReference(await file.file.LoadDocument(ct)));
				}
			}

			return documents;
		}

		#region Utilities

		/// <summary>
		/// Loads an XmlDocument from the given path.
		/// </summary>
		/// <param name="path"></param>
		/// <param name="ct"></param>
		/// <returns></returns>
		public static async Task<XmlDocument> LoadDocument(this string path, CancellationToken ct)
		{
#if WINDOWS_UWP
			var file = await StorageFile
				.GetFileFromPathAsync(path)
				.AsTask(ct);

			var document = await XmlDocument
				.LoadFromFileAsync(file, new XmlLoadSettings { ElementContentWhiteSpace = true })
				.AsTask(ct);
#else
			var document = new XmlDocument()
			{
				PreserveWhitespace = true,
			};

			document.Load(path);
#endif

			return document;
		}

		/// <summary>
		/// Save the given XmlDocument at the given path.
		/// </summary>
		/// <param name="document"></param>
		/// <param name="ct"></param>
		/// <param name="path"></param>
		/// <returns></returns>
		public static async Task Save(this XmlDocument document, CancellationToken ct, string path)
		{
#if WINDOWS_UWP
			var xml = document.GetXml();

			xml = Regex.Replace(xml, @"(<\? ?xml)(?<declaration>.+)( ?\?>)", x => !x.Groups["declaration"].Value.Contains("encoding", StringComparison.OrdinalIgnoreCase)
				? x.Result("$1${declaration} encoding=\"utf-8\"$2") // restore encoding declaration that is stripped by `GetXml`
				: x.Value
			);
			xml = Regex.Replace(xml, "(\\?>)(<)", "$1\n$2"); // xml declaration should follow by a new line
			xml = Regex.Replace(xml, "([^ ])(/>)", "$1 $2"); // self-enclosing tag should end with a space

			await FileIO.WriteTextAsync(await StorageFile.GetFileFromPathAsync(path).AsTask(ct), xml);
#else
			document.Save(path);
#endif
		}

		/// <summary>
		/// Select the XmlElements matching the given element name in the XmlDocument.
		/// </summary>
		/// <param name="document"></param>
		/// <param name="elementName">Name of the XML tags to look for.</param>
		/// <param name="filter">Addtional xpath filter to apply.</param>
		/// <returns></returns>
		public static IEnumerable<XmlElement> SelectElements(this XmlDocument document, string elementName, string filter = null) => document
			.SelectNodes($"//*[local-name() = '{elementName}']{filter}") //Using local-name to avoid having to deal with namespaces
			.OfType<XmlElement>();

		/// <summary>
		/// Select the first child node with the given of an element.
		/// </summary>
		/// <param name="element"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static XmlNode SelectNode(this XmlElement element, string name) => element
			.ChildNodes
			.OfType<XmlElement>()
			.FirstOrDefault(e => e.LocalName.ToString().Equals(name, StringComparison.OrdinalIgnoreCase)); //LocalName is not a string in UWP
		#endregion
	}
}
