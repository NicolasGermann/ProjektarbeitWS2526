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

            Console.WriteLine("[START] Lade XML");
            foreach (var a in XmlIterator.GetXmlPrinters(xmlPath))
            {
                Console.WriteLine("[START] Erzeuge Drucker");
                var printer = PrinterFactory.CreatePrinter((string?)a.Element("Name") ?? "");

                Console.WriteLine("[START] FillFromXml");
                printer = printer.FillFromXml(a);

                Console.WriteLine("[START] SetMessageFunctionDefault");
                printer = printer.SetMessageFunctionDefault();

                Console.WriteLine("[START] ConnectToBroker");
                printer = printer.ConnectToBroker();

                Console.WriteLine("[START] ConnectToDatabase");
                printer = printer.ConnectToDatabase(new InfluxDBDTO(host, token, bucket, org));
            }
            Console.WriteLine("[START] Startup abgeschlossen");

            while (true)
            {
                Console.WriteLine($"[HEARTBEAT] {DateTime.UtcNow:O}");
                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal startup error: {ex}");
            Environment.Exit(1);
        }
    }
}
