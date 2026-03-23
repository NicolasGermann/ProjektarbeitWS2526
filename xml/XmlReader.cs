using System.Xml.Linq;
using HTW.Result;
using HTW.Printer;

namespace HTW.XmlReaderExtention
{
	public static class XmlReader
	{
		public static PrinterDTO FillFromXml(PrinterDTO pr, XElement printer)
		{
			return new PrinterDTO
			{
				Name = (string?)printer?.Element("Name") ?? "",
				Host = (string?)printer?.Element("Host") ?? "",
				ID = (string?)printer?.Element("ID") ?? "",
				Port = (int?)printer?.Element("Port") ?? 0,
				Username = (string?)printer?.Element("Username") ?? "",
				Password = (string?)printer?.Element("Password") ?? ""
			};
		}
	}

	public static class XmlIterator
	{
		public static Result<IEnumerable<XElement>> GetXmlPrinters(string xmlPath)
		{
			try
			{
				var _doc = XDocument.Load(xmlPath);
				var dec = _doc.Descendants("Printer");
				return Result<IEnumerable<XElement>>.Some(dec);
			}
			catch (Exception e)
			{
				return Result<IEnumerable<XElement>>.None(e);
			}
		}

	}
	}
