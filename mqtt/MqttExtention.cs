using HTW.Printer;
using HTW.Connector;
using MQTTnet;


public static class MqttExtention
{
    static public PrinterDTO ConnectToBroker(this PrinterDTO pr, Func<MqttApplicationMessageReceivedEventArgs, Task> messageFunc)
    {
        var prnew = pr with {connector = new MqttConnector(pr.Host, pr.Port, pr.Username, pr.Password, messageFunc)};
        prnew.connector.ConnectAsync().ContinueWith(t => prnew.connector.SubscribeAsync(String.Format("device/{0}/report",pr.ID)));
	Console.WriteLine("Verbunden");
        return prnew;
    }

};
