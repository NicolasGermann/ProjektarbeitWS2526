using HTW.Printer;
using HTW.Connector;
using MQTTnet;
using System.Text;


public static class MqttExtention
{
    static public PrinterDTO SetMessageFunctionDefault(this PrinterDTO pr)
    {
        Func<MqttApplicationMessageReceivedEventArgs, Task> SaveToStack = t =>
        {
            pr.Messages.Enqueue(Encoding.UTF8.GetString(t.ApplicationMessage.Payload));
            return Task.CompletedTask;
        };
        return pr with { MessageFunction = SaveToStack };
    }

    static public PrinterDTO ConnectToBroker(this PrinterDTO pr)
    {
        var prnew = pr with { connector = new MqttConnector(pr.Host, pr.Port, pr.Username, pr.Password, pr.MessageFunction) };
        prnew.connector.ConnectAsync().ContinueWith(t => prnew.connector.SubscribeAsync(String.Format("device/{0}/report", pr.ID)));
        Console.WriteLine("Verbunden");
        return prnew;
    }

    static public PrinterDTO SetMessageFunction(this PrinterDTO pr, Func<MqttApplicationMessageReceivedEventArgs, Task> messFunc)
    {
        var prret = pr with { MessageFunction = messFunc };
        return prret;
    }
};
