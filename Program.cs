using HTW.Printer;
using HTW.XmlReaderExtention;
using System.Text;
using MQTTnet;

class Program
{


    static void Main()
    {
        Func<MqttApplicationMessageReceivedEventArgs, Task> printToConsole = t =>
        {
            Console.Write(String.Format("Message: {0}", Encoding.UTF8.GetString(t.ApplicationMessage.Payload)));
            return Task.CompletedTask;
        };


        PrinterFactory.CreatePrinter("Drucker1").LoadXml("testdata.xml").ConnectToBroker(printToConsole);
        PrinterFactory.CreatePrinter("Drucker2").LoadXml("testdata.xml").ConnectToBroker(printToConsole);
        while (true) { }
    }
}
