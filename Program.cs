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

        string host = Environment.GetEnvironmentVariable("DBHOST") ?? string.Empty;
        string token = Environment.GetEnvironmentVariable("DBTOKEN") ?? string.Empty;
        string bucket = Environment.GetEnvironmentVariable("BUCKET") ?? string.Empty;
        string org = Environment.GetEnvironmentVariable("ORG") ?? string.Empty;

        Func<MqttApplicationMessageReceivedEventArgs, Task> printToConsole = t =>
        {
            Console.Write(String.Format("Message: {0}", Encoding.UTF8.GetString(t.ApplicationMessage.Payload)));
            return Task.CompletedTask;
        };

        //var printers = XmlIterator.GetXmlPrinters("/home/docker-user/server/DataBridge-config/printer.xml")
        var printers = XmlIterator.GetXmlPrinters("/xml/printer.xml")
                    .TryBind(printers =>
                    {
                        foreach (var a in printers)
                        {
                            PrinterFactory
				.CreatePrinter((string?)a.Element("Name") ?? "").TryBind(buildLogger("CreatePrinter"))
				.FillFromXml(a).TryBind(buildLogger("FillFromXml"))
				.SetMessageFunctionDefault().TryBind(buildLogger("SetMessageFuncitonDefault"))
				.ConnectToBroker().TryBind(buildLogger("ConnectToBroker"))
				.ConnectToDatabase(new InfluxDBDTO(host, token, bucket, org))
				.TryBind(t =>
				{
				    t.database!.runnerThread!.Start();
				    return t;
				})
				.TryBind(buildLogger("ConnectToDatabase"))
				.CatchBind(e => Console.WriteLine($"[Fehler]: {e}"));
                        }
                        return printers;
                    });
        if (printers.error) Console.WriteLine($"[Fehler]: {printers.UnpackException()!}");

        while (true) { Thread.Sleep(1); }

    }
    public static Func<PrinterDTO, PrinterDTO> buildLogger(string s)
    {
        Func<PrinterDTO, PrinterDTO> LogStep = t =>
        {
            Console.WriteLine($"[Start] {s} durchgeführt: {t.Name}");
            return t;
        };
        return LogStep;

    }
}
