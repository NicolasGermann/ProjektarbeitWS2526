using HTW.Influx.Database;
using HTW.Printer;
using HTW.Influx.DataConverter;
using InfluxDB3.Client;
using InfluxDB3.Client.Write;
using HTW.Result;

namespace HTW.Influx.Database
{
    public record InfluxDB(string host, string token, string database, InfluxDBClient? dbClient = null, Thread? runnerThread = null);
}

namespace HTW.Influx.Extention
{
    public static class InfluxExtention
    {
        public static PrinterDTO ConnectToDatabase(this PrinterDTO pr, InfluxDB db)
        {
            var dbc = new InfluxDBClient(db.host, db.token, db.database);
            Thread thread = new Thread(async t =>
            {
                while (true)
                {
                    if (pr.Messages.Count() > 0)
                    {
                        var msg =pr.Messages.Dequeue();
                        Result.Result<PointData> dataPoint = JasonToInflux.JsonToInfluxPoint(msg, pr);
                        switch (dataPoint)
                        {
                            case Result<PointData>.Success(var a):
                                await dbc.WritePointAsync(a, db.database);
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
