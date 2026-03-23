using System.Xml.Linq;
using HTW.Result;
using HTW.Printer;

namespace HTW.XmlReaderExtention
{
    public static class XmlReader
    {
        public static PrinterDTO FillFromXml(PrinterDTO pr, XElement printer)
        {
            pr.Name = (string?)printer?.Element("Name") ?? "";
            pr.Host = (string?)printer?.Element("Host") ?? "";
            pr.ID = (string?)printer?.Element("ID") ?? "";
            pr.Port = (int?)printer?.Element("Port") ?? 0;
            pr.Username = (string?)printer?.Element("Username") ?? "";
            pr.Password = (string?)printer?.Element("Password") ?? "";
            return pr;

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
