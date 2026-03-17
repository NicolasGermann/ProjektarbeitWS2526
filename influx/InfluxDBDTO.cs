using InfluxDB.Client;

namespace HTW.Influx.Database
{
    public record InfluxDBDTO(
        string host,
        string token,
        string bucket,
        string org,
        InfluxDBClient? dbClient = null,
        Thread? runnerThread = null);
}