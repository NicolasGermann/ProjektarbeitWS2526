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
        string token =  Environment.GetEnvironmentVariable("DBTOKEN") ?? string.Empty;
        string bucket =  Environment.GetEnvironmentVariable("BUCKET") ?? string.Empty;
        string org =  Environment.GetEnvironmentVariable("ORG") ?? string.Empty;
        
        Func<MqttApplicationMessageReceivedEventArgs, Task> printToConsole = t =>
        {
            Console.Write(String.Format("Message: {0}", Encoding.UTF8.GetString(t.ApplicationMessage.Payload)));
            return Task.CompletedTask;
        };

        foreach (var a in XmlIterator.GetXmlPrinters("/home/docker-user/server/DataBridge-config/printer.xml"))
        {
            PrinterFactory.CreatePrinter((string?)a.Element("Name") ?? "").FillFromXml(a).SetMessageFunctionDefault().ConnectToBroker().ConnectToDatabase(new InfluxDBDTO(host,token,bucket,org));
        }


        while (true) { }

    }
}
