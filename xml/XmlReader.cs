using System.Xml.Linq;
using HTW.Printer;

namespace HTW.XmlReaderExtention
{
    public static class XmlReaderExtention
    {
	public static PrinterDTO FillFromXml(this PrinterDTO pr, XElement printer){
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

    public static IEnumerable<XElement> GetXmlPrinters(string xmlPath)
    {
        if (!File.Exists(xmlPath))
            throw new FileNotFoundException($"printer.xml nicht gefunden: {xmlPath}");

        var doc = XDocument.Load(xmlPath);
        return doc.Descendants("Printer");
    }

}
