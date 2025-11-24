using HTW.Connector;
using HTW.Influx.Database;
using MQTTnet;

namespace HTW.Printer
{
    public record PrinterDTO
    {
        public string Name { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string ID { get; set; } = string.Empty;
        public int Port { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public MqttConnector? connector { get; set; }
        public InfluxDB? database { get; set; }
        public Func<MqttApplicationMessageReceivedEventArgs, Task> MessageFunction { get; set; } = t => Task.CompletedTask;
        public Queue<string> Messages { get; set; } = new Queue<string>();
    };

    public static class PrinterFactory
    {
        public static PrinterDTO CreatePrinter(string Name)
        {
            return new PrinterDTO() { Name = Name };
        }
    }
}
