//Gets all Fields from influx with an given Job ID and save them to an csv file in /tmp
/*
var exporter = new HTW.Influx.Export.JobCsvExporter(
    new InfluxDBDTO(host, token, bucket, org));

var csvPath = await exporter.ExportJobToCsvAsync("P1S1", "job-123");
Console.WriteLine(csvPath);
*/

using System.Globalization;
using System.Text;
using HTW.Influx.Database;
using InfluxDB.Client;
using InfluxDB.Client.Core.Flux.Domain;

namespace HTW.Influx.Export
{
    public sealed class JobCsvExporter
    {
        private readonly InfluxDBDTO _db;
        private readonly string _tempDirectory;

        public JobCsvExporter(InfluxDBDTO db, string tempDirectory = "/tmp")
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _tempDirectory = string.IsNullOrWhiteSpace(tempDirectory)
                ? throw new ArgumentException("tempDirectory darf nicht leer sein.", nameof(tempDirectory))
                : tempDirectory;
        }

        public async Task<string> ExportJobToCsvAsync(
            string printerName,
            string jobId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("printerName darf nicht leer sein.", nameof(printerName));

            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException("jobId darf nicht leer sein.", nameof(jobId));

            Directory.CreateDirectory(_tempDirectory);

            var measurement = $"Printer Data: {printerName}";
            var flux = BuildFluxQuery(_db.bucket, measurement, jobId);

            var ownsClient = _db.dbClient is null;
            using var client = ownsClient ? new InfluxDBClient(_db.host, _db.token) : _db.dbClient!;
            var queryApi = client.GetQueryApi();

            var tables = await queryApi.QueryAsync(flux, _db.org, cancellationToken);
            var records = tables
                .SelectMany(t => t.Records)
                .OrderBy(r => r.GetTime())
                .ToList();

            if (records.Count == 0)
                throw new InvalidOperationException(
                    $"Keine Daten für Drucker '{printerName}' und jobId '{jobId}' gefunden.");

            var safePrinter = SanitizeFileName(printerName);
            var safeJobId = SanitizeFileName(jobId);
            var filePath = Path.Combine(_tempDirectory, $"{safePrinter}_{safeJobId}.csv");

            await using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
            await writer.WriteLineAsync("time;measurement;field;value;job_id");

            foreach (var record in records)
            {
                var time = record.GetTime()?.ToDateTimeUtc().ToString("O", CultureInfo.InvariantCulture) ?? "";
                var measurementName = GetValue(record, "_measurement") ?? measurement;
                var field = record.GetField() ?? "";
                var value = ConvertValue(record.GetValue());
                var resolvedJobId = GetValue(record, "job_id") ?? jobId;

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

        private static string BuildFluxQuery(string bucket, string measurement, string jobId)
        {
            return
$@"from(bucket: ""{EscapeFlux(bucket)}"")
  |> range(start: 0)
  |> filter(fn: (r) => r[""_measurement""] == ""{EscapeFlux(measurement)}"")
  |> filter(fn: (r) => r[""job_id""] == ""{EscapeFlux(jobId)}"")
  |> sort(columns: [""_time""])";
        }

        private static string? GetValue(FluxRecord record, string key)
        {
            return record.Values.TryGetValue(key, out var value)
                ? value?.ToString()
                : null;
        }

        private static string ConvertValue(object? value)
        {
            return value switch
            {
                null => "",
                DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
        }

        private static string EscapeCsv(string input)
        {
            if (input.Contains(';') || input.Contains('"') || input.Contains('\n') || input.Contains('\r'))
                return "\"" + input.Replace("\"", "\"\"") + "\"";

            return input;
        }

        private static string EscapeFlux(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
        }
    }
}