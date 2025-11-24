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
        Func<MqttApplicationMessageReceivedEventArgs, Task> printToConsole = t =>
        {
            Console.Write(String.Format("Message: {0}", Encoding.UTF8.GetString(t.ApplicationMessage.Payload)));
            return Task.CompletedTask;
        };

        foreach (var a in XmlIterator.GetXmlPrinters("testdata.xml"))
        {
            PrinterFactory.CreatePrinter((string?)a.Element("Name") ?? "").FillFromXml(a).SetMessageFunctionDefault().ConnectToBroker().ConnectToDatabase(new InfluxDB("http://localhost:8181","","bydb"));
        }


        while (true) { }

    }
}
