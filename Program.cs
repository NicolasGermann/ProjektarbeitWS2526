using HTW.Printer;
using HTW.Result;
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

		var printers = XmlIterator.GetXmlPrinters("/home/docker-user/server/DataBridge-config/printer.xml")
		//		var printers = XmlIterator.GetXmlPrinters("/xml/printer.xml")

		    .Bind(printers =>
		    {
			    foreach (var a in printers)
			    {
				    var setup = PrinterFactory
					.CreatePrinter((string?)a.Element("Name") ?? "").Bind(buildLogger("CreatePrinter"))
					.FillFromXml(a).Bind(buildLogger("FillFromXml"))
					.SetMessageFunctionDefault().Bind(buildLogger("SetMessageFuncitonDefault"))
					.ConnectToBroker().Bind(buildLogger("ConnectToBroker"))
					.ConnectToDatabase(new InfluxDBDTO(host, token, bucket, org)).Bind(buildLogger("ConnectToDatabase"));
				    if (setup.error) Console.WriteLine($"[Fehler]: {setup.UnpackException()!}");
			    }
			    return Result<Object>.Some(new Object());
		    });
		if (printers.error) Console.WriteLine($"[Fehler]: {printers.UnpackException()!}");

		while (true) { Thread.Sleep(1); }

	}
	public static Func<PrinterDTO, Result<PrinterDTO>> buildLogger(string s)
	{

		Func<PrinterDTO, Result<PrinterDTO>> LogStep = t =>
		{
			Console.Write($"[Start] {s} durchgeführt");
			return Result<PrinterDTO>.Some(t);
		};
		return LogStep;

	}
}
