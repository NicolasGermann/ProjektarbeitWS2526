using HTW.Connector;
using HTW.Influx.Database;
using MQTTnet;
using System.Collections.Concurrent;

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
        public InfluxDBDTO? database { get; set; }
        public Func<MqttApplicationMessageReceivedEventArgs, Task> MessageFunction { get; set; } = t => Task.CompletedTask;
        public ConcurrentQueue<string> Messages { get; set; } = new();

        public string? CurrentJobId { get; set; }
        public string? CurrentTaskId { get; set; }
        public string? CurrentProjectId { get; set; }
        public string? CurrentSubtaskName { get; set; }
        public string? CurrentGcodeState { get; set; }

        public string? LastFinishedJobId { get; set; }
        public string? LastExportedJobId { get; set; }
    };

    public static class PrinterFactory
    {
        public static PrinterDTO CreatePrinter(string Name)
        {
            return new PrinterDTO() { Name = Name };
        }
    }
}
