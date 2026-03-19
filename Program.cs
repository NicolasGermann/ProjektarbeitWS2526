using HTW.Printer;
using HTW.XmlReaderExtention;
using System.Text;
using MQTTnet;
using HTW.Influx.Extention;
using HTW.Influx.Database;

class Program
{


    static void Main()
    {
        try
        {
            string host = Environment.GetEnvironmentVariable("DBHOST") ?? string.Empty;
            string token = Environment.GetEnvironmentVariable("DBTOKEN") ?? string.Empty;
            string bucket = Environment.GetEnvironmentVariable("BUCKET") ?? string.Empty;
            string org = Environment.GetEnvironmentVariable("ORG") ?? string.Empty;

            var xmlPath = "/home/docker-user/server/DataBridge-config/printer.xml";
            Console.WriteLine($"Loading printer config from: {xmlPath}");

            foreach (var a in XmlIterator.GetXmlPrinters(xmlPath))
            {
                PrinterFactory.CreatePrinter((string?)a.Element("Name") ?? "")
                    .FillFromXml(a)
                    .SetMessageFunctionDefault()
                    .ConnectToBroker()
                    .ConnectToDatabase(new InfluxDBDTO(host, token, bucket, org));
            }

            while (true)
            {
                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal startup error: {ex}");
            Environment.Exit(1);
        }
    }
}
