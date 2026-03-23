using HTW.Influx.Database;
using HTW.Influx.DataConverter;
using HTW.Printer;
using InfluxDB.Client;
using InfluxDB.Client.Writes;

namespace HTW.Influx.Database
{
    public record InfluxDBDTO(string host, string token, string bucket, string org, InfluxDBClient? dbClient = null, Thread? runnerThread = null);
}

namespace HTW.Influx.Extention
{
    public static class InfluxExtention
    {
        public static PrinterDTO ConnectToDatabase(PrinterDTO pr, InfluxDBDTO db)
        {
            var dbc = new InfluxDBClient(db.host, db.token);
            Console.WriteLine($"{db.host}, {db.token}");
            pr.database = db with { dbClient = dbc };
            return pr;
        }

        public static PrinterDTO createDBThread(PrinterDTO pr)
        {
            var db = pr.database!;
            var dbc = pr.database!.dbClient!;
            Thread thread = new Thread(async _ =>
                {
                    var b = await dbc.PingAsync();
                    var writeApi = dbc.GetWriteApi();
                    Console.WriteLine($"DB Connection: {b}");
                    while (true)
                    {
                        Thread.Sleep(10);
                        if (pr.Messages.Count() > 0)
                        {
                            var msg = pr.Messages.Dequeue();
                            PointData dataPoint;
                            try
                            {
                                dataPoint = JsonToInflux.JsonToInfluxPoint(msg, pr);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"[Influx] Punkt konnte nicht konvertiert werden: {e}");
                                continue;
                            }
                            writeApi.WritePoint(dataPoint, db.bucket, db.org);
                            writeApi.Flush();
                            Console.WriteLine($"[Influx] datenpunkt geschrieben: {pr.Name},{dataPoint}");
                        }
                    }
                });

            pr.database = db with { runnerThread = thread };
            return pr;
        }
    }
}
