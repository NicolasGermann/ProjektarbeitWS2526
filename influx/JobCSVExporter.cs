using HTW.Result;
using HTW.Influx.Database;
using HTW.Printer;
using System.Text;
using InfluxDB.Client.Core.Flux.Domain;
using System.Globalization;
using HTW.IO;
using HTW.Images;

namespace HTW.Influx.Export
{
    public static class JobCsvExporter
    {

	public static PrinterDTO createCsvThread(PrinterDTO printer){
	    
                            printer.CsvThread = new Thread(async _ =>
                            {
				try
				{
                                    ThreeMfPreviewDownloader.TryDownloadPreview(printer);
                                }
				catch
				{
				    
				}
				try
				{
                                    ThreeMfToSmbCopier.TryCopyThreeMfToSmb(printer);
                                }
				catch
				{
                                    Console.WriteLine($"[ThreeMftoSmb] Copying 3mf File failed");
                                }
                                try
                                {
                                    Thread.Sleep(500);
                                    var path = await JobCsvExporter.exportCSV(printer.database!.bucket
                                    , $"Printer Data: {printer.Name}"
                                    , printer
                                    , $"{printer.lastJobId}");

                                    var copier = new CSVFileCopier(path);
                                    var result = copier.CopyToJobFolder(printer.Name, $"{printer.lastJobId}");
                                    Console.WriteLine($"[CsvExporter] Csv Exported and copied.");
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"[JsonToInflux] CsvExport fehlgeschlagen: {printer.lastJobId}--{e}");
                                }
                            });
			    return printer;
	}

        public static async Task<string> exportCSV(string bucket, string measurement, PrinterDTO printer, string jobId, CancellationToken cancellationToken = default, string tempDirectory = "/tmp")
        {
            Func<string, string> EscapeFlux = value => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

            var query = @$"from(bucket: ""{EscapeFlux(bucket)}"")
				|> range(start: 0)
				|> filter(fn: (r) => r[""_measurement""] == ""{EscapeFlux(measurement)}"")
				|> filter(fn: (r) => r[""job_id""] == ""{EscapeFlux(jobId)}"")
				|> sort(columns: [""_time""])";

            Directory.CreateDirectory(tempDirectory);
            if (printer.database == null) throw (new Exception("[JobCsvExporter] es wurde keine Datenbank gesetzt."));
            if (printer.database.dbClient == null) throw (new Exception("[JobCsvExporter] es wurde keine DatenbankClient gesetzt."));

	    Func<string, string> SanitizeFileName = value =>
	    {
		var cleaned = new string(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
		return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
	    };
	    Func<(FluxRecord, string), string?> GetValue = tup =>
	    {
		return tup.Item1.Values.TryGetValue(tup.Item2, out var value)
		    ? value?.ToString()
		    : null;
	    };
	    Func<object?, string> ConvertValue = value =>
	    {
		return value switch
		{
		    null => "",
		    DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
		    IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
		    _ => value.ToString() ?? ""
		};
	    };
	    Func<string, string> EscapeCsv = i => (i.Contains(';') || i.Contains('"') || i.Contains('\n') || i.Contains('\r')) ? "\"" + i.Replace("\"", "\"\"") + "\"" : i;

	    var queryApi = printer.database.dbClient.GetQueryApi();
	    var tables = await queryApi.QueryAsync(query , printer.database!.org!, cancellationToken);
	    var records = tables
		.SelectMany(t => t.Records)
		.OrderBy(r => r.GetTime())
		.ToList();
	    if (!records.Any()) throw new Exception($"[JobCsvExporter] Keine Daten für Drucker {printer.Name} und JobId {jobId} gefunden");

	    var safePrinter = SanitizeFileName(printer.Name);
	    var safeJobId = SanitizeFileName(jobId);
	    var filePath = Path.Combine(tempDirectory, $"{safePrinter}_{safeJobId}.csv");

	    await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
	    await writer.WriteLineAsync("time;measurement;field;value;job_id");

	    var mMent = $"Printer Data: {printer.Name}";
	    foreach (var record in records)
	    {
		var time = record.GetTime()?.ToDateTimeUtc().ToString("O", CultureInfo.InvariantCulture) ?? "";
		var measurementName = GetValue((record, "_measurement")) ?? mMent;
		var field = record.GetField() ?? "";
		var value = ConvertValue(record.GetValue());
		var resolvedJobId = GetValue((record, "job_id")) ?? jobId;

		var row = string.Join(";",
		EscapeCsv(time),
		EscapeCsv(measurementName),
		EscapeCsv(field),
		EscapeCsv(value),
		EscapeCsv(resolvedJobId));

		await writer.WriteLineAsync(row);
	    }
	    await writer.FlushAsync();
            return filePath;
        }
    }
}
