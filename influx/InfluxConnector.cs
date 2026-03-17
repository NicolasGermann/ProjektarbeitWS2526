using HTW.Influx.Database;
using HTW.Printer;
using HTW.Influx.DataConverter;
using InfluxDB.Client;
using HTW.Result;
using InfluxDB.Client.Writes;

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
                    try
                    {
                        if (pr.Messages.TryDequeue(out var msg))
                        {
                            Result.Result<PointData> dataPoint = JasonToInflux.JsonToInfluxPoint(msg, pr);

                            switch (dataPoint)
                            {
                                case Result<PointData>.Success(var a):
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
                                    Console.WriteLine($"Fehler im Influx Adapter: {a}");
                                    break;

                                default:
                                    break;
                            }
                        }
                        else
                        {
                            Thread.Sleep(10);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"INFLUX RUNNER ERROR: {ex}");
                        Thread.Sleep(100);
                    }
                }
            });

            thread.IsBackground = true;
            thread.Start();

            return pr with { database = db with { dbClient = dbc, runnerThread = thread } };
        }
    }
}