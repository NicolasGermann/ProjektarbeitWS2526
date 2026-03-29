using HTW.Influx.Database;
using HTW.Influx.DataConverter;
using HTW.Printer;
using HTW.Result;
using InfluxDB.Client;

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
                            var msg = Result<string>.Some(pr.Messages.Dequeue());
                            msg.TryBind(m => JsonToInflux.JsonToInfluxPoint(m, pr))
                            .TryBind(dp =>
                            {
                                writeApi.WritePoint(dp, db.bucket, db.org);
                                writeApi.Flush();
                                Console.WriteLine($"[Influx]datenpunkt geschrieben: {pr.Name}-{pr.lastJobId ?? "Keine JobId empfangen"}-,{dp.ToLineProtocol().Replace("\r", "@").Replace("\n", "@")}");
                                return dp;
                            })
			    .TryBind(dp =>
			    {
                                //Schreiben der CSV-Dateien.
                                Directory.CreateDirectory("/logs");
                                File.AppendAllText($"/logs/{pr.lastJobId ?? "NoID"}.txt", dp.ToLineProtocol());
                                File.AppendAllText($"/logs/{pr.lastJobId ?? "NoID"}.txt", "\n");
                                return dp;
                            })
			    .CatchBind(e =>
			    {
				Console.WriteLine($"[Error]({DateTime.UtcNow})Influx: Punkt konnte nicht konvertiert werden: {e}");
			    });
                        }
                    }
                });

            pr.database = db with { runnerThread = thread };
            return pr;
        }
    }
}
