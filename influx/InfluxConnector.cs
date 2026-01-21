using HTW.Influx.Database;
using HTW.Printer;
using HTW.Influx.DataConverter;
using InfluxDB.Client;
using HTW.Result;
using InfluxDB.Client.Writes;

namespace HTW.Influx.Database
{
    public record InfluxDBDTO(string host, string token, string bucket, string org, InfluxDBClient? dbClient = null, Thread? runnerThread = null);
}

namespace HTW.Influx.Extention
{
    public static class InfluxExtention
    {
        public static PrinterDTO ConnectToDatabase(this PrinterDTO pr, InfluxDBDTO db)
        {

            var dbc = new InfluxDBClient(db.host, db.token);
            var writeApi = dbc.GetWriteApi();
            Console.WriteLine($"{db.host}, {db.token}");
            Thread thread = new Thread(async _ =>
                {
                    var b = await dbc.PingAsync();
                    Console.WriteLine($"DB Connection: {b}");
                    while (true)
                    {
                        Thread.Sleep(10);
                        if (pr.Messages.Count() > 0)
                        {
                            var msg = pr.Messages.Dequeue();
                            Result.Result<PointData> dataPoint = JasonToInflux.JsonToInfluxPoint(msg, pr);
                            switch (dataPoint)
                            {
                                case Result<PointData>.Success(var a):
                                    Console.WriteLine($"Influx: {a.ToLineProtocol()}");
                                    try
                                    {
                                        writeApi.WritePoint(a, db.bucket, db.org);
                                        writeApi.Flush();
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine($"INFLUX: {e}");
                                    }
                                    break;
                                case Result<PointData>.Failure(var a):
                                    Console.WriteLine(String.Format("Fehler im Influx Adapter: {0}", a));
                                    break;
                                default:
                                    break;
                            }

                        }
                    }

                });
            thread.Start();


            return pr with { database = db with { dbClient = dbc, runnerThread = thread } };
        }
    }
}
