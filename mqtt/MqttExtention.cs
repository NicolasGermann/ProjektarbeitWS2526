using HTW.Printer;
using HTW.Connector;
using HTW.Result;
using MQTTnet;
using System.Text;


public static class MqttExtention
{
	static public Result<PrinterDTO> SetMessageFunctionDefault(this Result<PrinterDTO> pr)
	{
		if (pr.error) return pr;
		var prn = pr.UnpackValue()!;
		Func<MqttApplicationMessageReceivedEventArgs, Task> SaveToStack = t =>
		{
			prn.Messages.Enqueue(Encoding.UTF8.GetString(t.ApplicationMessage.Payload));
			Console.WriteLine($"MQTT: {Encoding.UTF8.GetString(t.ApplicationMessage.Payload)}");
			return Task.CompletedTask;
		};
		return Result<PrinterDTO>.Some(prn with { MessageFunction = SaveToStack });
	}

	static public Result<PrinterDTO> ConnectToBroker(this Result<PrinterDTO> pr)
	{
		if (pr.error) return pr;
		var prn = pr.UnpackValue()!;
		try
		{
			var prnew = prn with { connector = new MqttConnector(prn.Host, prn.Port, prn.Username, prn.Password, prn.MessageFunction) };
			prnew.connector.ConnectAsync().ContinueWith(t => prnew.connector.SubscribeAsync(String.Format("device/{0}/report", prn.ID)));
			Console.WriteLine("Verbunden");
			return Result<PrinterDTO>.Some(prnew);
		}
		catch (Exception e)
		{
			return Result<PrinterDTO>.None(e);
		}
	}

	static public Result<PrinterDTO> SetMessageFunction(this Result<PrinterDTO> pr, Func<MqttApplicationMessageReceivedEventArgs, Task> messFunc)
	{
		if (pr.error) return pr;
		var prn = pr.UnpackValue()!;
		var prret = prn with { MessageFunction = messFunc };
		return Result<PrinterDTO>.Some(prret);
	}
};
