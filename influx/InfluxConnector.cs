using HTW.Influx.Database;
using HTW.Influx.DataConverter;
using HTW.Printer;
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
		public static Result<PrinterDTO> ConnectToDatabase(this Result<PrinterDTO> pr, InfluxDBDTO db)
		{
			if (pr.error) return pr;
			var prn = pr.UnpackValue()!;
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
					    if (prn.Messages.Count() > 0)
					    {
						    var msg = prn.Messages.Dequeue();
						    Result<PointData> dataPoint = JsonToInflux.JsonToInfluxPoint(msg, prn);
						    dataPoint.Bind(a =>
						    {
							    try
							    {
								    writeApi.WritePoint(a, db.bucket, db.org);
								    writeApi.Flush();
								    return Result<PointData>.Some(a);
							    }
							    catch (Exception e)
							    {
								    return Result<PointData>.None(e);
							    }
						    });
						    switch (dataPoint.error)
						    {
							    case false:
								    break;
							    case true:
								    Console.WriteLine(String.Format("Fehler im Influx Adapter: {0}", dataPoint.UnpackException()));
								    break;
						    }

					    }
				    }

			    });
			thread.Start();

			return Result<PrinterDTO>.Some(prn with { database = db with { dbClient = dbc, runnerThread = thread } });
		}
	}
}
