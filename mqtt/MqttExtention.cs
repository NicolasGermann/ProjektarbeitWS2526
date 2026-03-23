using HTW.Printer;
using HTW.Connector;
using HTW.Result;
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
        pr.MessageFunction = SaveToStack;
        return pr ;
    }

    static public PrinterDTO ConnectToBroker(this PrinterDTO pr)
    {
        pr.connector = new MqttConnector(pr.Host, pr.Port, pr.Username, pr.Password, pr.MessageFunction);
	pr.connector!.ConnectAsync().ContinueWith(t => pr.connector.SubscribeAsync(String.Format("device/{0}/report", pr.ID)));
	Console.WriteLine("Verbunden");
        return pr;
    }

    static public Result<PrinterDTO> SetMessageFunction(this Result<PrinterDTO> pr, Func<MqttApplicationMessageReceivedEventArgs, Task> messFunc)
    {
        if (pr.error) return pr;
        var prn = pr.UnpackValue()!;
        var prret = prn with { MessageFunction = messFunc };
        return Result<PrinterDTO>.Some(prret);
    }
};
