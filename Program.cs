using HTW.Printer;
using HTW.XmlReaderExtention;
using System.Text;
using MQTTnet;
using HTW.Influx.Extention;
using HTW.Influx.Database;
using HTW.Result;
using HTW.Influx.Export;

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
	
        var printers = XmlIterator.GetXmlPrinters("/home/docker-user/server/DataBridge-config/printer.xml")
                    .TryBind(printers =>
                    {
                        foreach (var a in printers)
                        {

                            Result<PrinterDTO>.Some(PrinterFactory.CreatePrinter((string?)a.Element("Name") ?? ""))
				.TryBind(buildLogger("CreatePrinter"))
				.TryBind(p => XmlReader.FillFromXml(p, a))
				.TryBind(buildLogger("FillFromXml"))
				.TryBind(p => MqttExtention.SetMessageFunctionDefault(p))
				.TryBind(buildLogger("SetMessageFuncitonDefault"))
				.TryBind(p => MqttExtention.ConnectToBroker(p))
				.TryBind(buildLogger("ConnectToBroker"))
				.TryBind(p => InfluxExtention.ConnectToDatabase(p, new InfluxDBDTO(host, token, bucket, org)))
				.TryBind(buildLogger("ConnectToDatabase"))
				.TryBind(p => JobCsvExporter.createCsvThread(p))
				.TryBind(buildLogger("createCsvThread"))
				.TryBind(p => InfluxExtention.createDBThread(p))
				.TryBind(t =>
				{
				    t.database!.runnerThread!.Start();
				    return t;
				})
				.TryBind(buildLogger("ConnectToDatabase"))
				.CatchBind(e => Console.WriteLine($"[Error]({DateTime.UtcNow}): {e}"));
                        }
                        return printers;
                    })
		    .CatchBind(e => Console.WriteLine($"[Error]({DateTime.UtcNow}): {e}"));

        while (true) {
	    Thread.Sleep(1);
        }

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
