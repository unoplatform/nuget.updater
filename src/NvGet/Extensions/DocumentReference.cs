using System;
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
	public abstract class DocumentReference
	{
		public abstract Task Save(CancellationToken ct, string path);
	}
}
