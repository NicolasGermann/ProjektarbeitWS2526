using HTW.Influx.Database;
using HTW.Printer;
using HTW.Influx.DataConverter;
using InfluxDB.Client;
using HTW.Result;
using InfluxDB.Client.Writes;

namespace HTW.Influx.Extention {
public static class InfluxExtention {
    public static PrinterDTO ConnectToDatabase(this PrinterDTO pr, InfluxDBDTO db)
    {
        var dbc = new InfluxDBClient(db.host, db.token);
        var writeApi = dbc.GetWriteApi();

        Console.WriteLine($"{db.host}, {db.token}");

        Thread thread = new Thread(async
                                       _ =>
            {
                var b = await dbc.PingAsync();
                Console.WriteLine($"DB Connection: {b}");

                while (true) {
                    try {
                        if (pr.Messages.TryDequeue(out var msg)) {
                            var result = JasonToInflux.JsonToInfluxPoint(msg, pr);

                            switch (result) {
        case Result<(PointData Point, string Measurement, List<string> FieldNames, string? JobId, string? GcodeState)>.Success(var built):
            try {
                writeApi.WritePoint(built.Point, db.bucket, db.org);
                writeApi.Flush();
                var jobLabel = string.IsNullOrWhiteSpace(built.JobId) ? "no-job" : built.JobId;

                Console.WriteLine($"[INFLUX] job_id={jobLabel} measurement=\"{built.Measurement}\" fields=[{string.Join(", ", built.FieldNames)}]");
            } catch (Exception e) {
                Console.WriteLine($"[INFLUX] job_id={(built.JobId ?? "-")} fields=[{string.Join(", ", built.FieldNames)}]");
            }
            break;

        case Result<(PointData Point, string Measurement, List<string> FieldNames, string? JobId, string? GcodeState)>.Failure(var error):
            Console.WriteLine($"[INFLUX] Adapter Fehler: {error}");
            break;
        }
                        } else {
                            Thread.Sleep(10);
                        }
                    } catch (Exception ex) {
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