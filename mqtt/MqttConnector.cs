using MQTTnet;

namespace HTW.Connector
{

    public class MqttConnector
    {
        private IMqttClient _client;
        private MqttClientOptions _options;

        public MqttConnector(string brokerHost, int brokerPort, string username, string password, Func<MqttApplicationMessageReceivedEventArgs, Task> messageFunc)
        {
            var factory = new MqttClientFactory();
            _client = factory.CreateMqttClient();

            _options = new MqttClientOptionsBuilder()
                .WithTcpServer(brokerHost, brokerPort)
                .WithCredentials(username, password)
                .WithCleanSession()
                .Build();

            _client.ApplicationMessageReceivedAsync += messageFunc;


        }

        async public Task ConnectAsync()
        {
            try
            {
                await this._client.ConnectAsync(this._options);
            }
            catch (Exception e)
            {
                Console.WriteLine($"MQTT: {e}");
            }
        }

        async public Task SubscribeAsync(string topic)
        {
            try
            {
                await this._client.SubscribeAsync(topic);
            }
            catch (Exception e)
            {
                Console.WriteLine($"MQTT: {e}");
            }
        }

    }
}
