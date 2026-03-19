using System.Xml.Linq;
using HTW.Result;
using HTW.Printer;

namespace HTW.XmlReaderExtention
{
	public static class XmlReaderExtention
	{
		public static Result<PrinterDTO> FillFromXml(this Result<PrinterDTO> pr, XElement printer)
		{
			if (pr.error) return pr;
			return Result<PrinterDTO>.Some(new PrinterDTO
			{
				Name = (string?)printer?.Element("Name") ?? "",
				Host = (string?)printer?.Element("Host") ?? "",
				ID = (string?)printer?.Element("ID") ?? "",
				Port = (int?)printer?.Element("Port") ?? 0,
				Username = (string?)printer?.Element("Username") ?? "",
				Password = (string?)printer?.Element("Password") ?? ""
			});

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
