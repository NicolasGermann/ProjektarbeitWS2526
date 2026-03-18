using HTW.Influx.Database;
using HTW.Printer;
using HTW.Influx.DataConverter;
using HTW.Influx.Export;
using Projektarbeit.IO;
using InfluxDB.Client;
using HTW.Result;
using InfluxDB.Client.Writes;
using HTW.Images;

namespace HTW.Influx.Extention
{
    public static class InfluxExtention
    {
        public static PrinterDTO ConnectToDatabase(this PrinterDTO pr, InfluxDBDTO db)
        {
            var dbc = new InfluxDBClient(db.host, db.token);
            var writeApi = dbc.GetWriteApi();

            Thread thread = new Thread(_ =>
            {
                try
                {
                    var b = dbc.PingAsync().GetAwaiter().GetResult();
                    Console.WriteLine($"[INFLUX] printer={pr.Name} serial={pr.ID} DB Connection: {b}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[INFLUX] Ping Fehler: {ex.Message}");
                }

                while (true)
                {
                    try
                    {
                        if (pr.Messages.TryDequeue(out var msg))
                        {
                            var result = JsonToInflux.JsonToInfluxPoint(msg, pr);

                            switch (result)
                            {
                                case Result<(PointData Point, string Measurement, List<string> FieldNames, string? JobId, string? GcodeState)>.Success(var built):
                                    try
                                    {
                                        writeApi.WritePoint(built.Point, db.bucket, db.org);
                                        writeApi.Flush();

                                        var effectiveJobId = built.JobId ?? pr.LastFinishedJobId;
                                        var jobLabel = string.IsNullOrWhiteSpace(effectiveJobId) ? "no-job" : effectiveJobId;

                                        Console.WriteLine(
                                            $"[INFLUX] job_id={jobLabel} fields=[{string.Join(", ", built.FieldNames)}]");

                                        TryDownloadPreview(pr);
                                        TryExportFinishedJob(pr, db);
                                    }
                                    catch (Exception e)
                                    {
                                        Console.WriteLine($"[INFLUX] Write Fehler: {e.Message}");
                                    }
                                    break;

                                case Result<(PointData Point, string Measurement, List<string> FieldNames, string? JobId, string? GcodeState)>.Failure(var error):
                                    Console.WriteLine($"[INFLUX] Adapter Fehler: {error}");
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
                        Console.WriteLine($"[INFLUX] Runner Fehler: {ex.Message}");
                        Thread.Sleep(100);
                    }
                }
            });

            thread.IsBackground = true;
            thread.Start();

            return pr with { database = db with { dbClient = dbc, runnerThread = thread } };
        }

        private static void TryExportFinishedJob(PrinterDTO pr, InfluxDBDTO db)
        {
            var finishedJobId = pr.LastFinishedJobId;

            if (string.IsNullOrWhiteSpace(finishedJobId))
                return;

            if (string.Equals(pr.LastExportedJobId, finishedJobId, StringComparison.Ordinal))
                return;

            try
            {
                Thread.Sleep(500);

                var exporter = new JobCsvExporter(db);
                var csvPath = exporter.ExportJobToCsvAsync(pr.Name, finishedJobId)
                    .GetAwaiter()
                    .GetResult();

                var copier = new CSVFileCopier(csvPath);
                var copiedPath = copier.CopyWithTimestamp();

                pr.LastExportedJobId = finishedJobId;
                pr.LastFinishedJobId = null;

                Console.WriteLine(
                    $"[JOB-EXPORT] printer={pr.Name} serial={pr.ID} job_id={finishedJobId} csv={copiedPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[JOB-EXPORT] Fehler bei printer={pr.Name} serial={pr.ID} job_id={finishedJobId}: {ex.Message}");
            }
        }

        private static void TryDownloadPreview(PrinterDTO pr)
        {
            var url = pr.CurrentThreeMfUrl;

            if (string.IsNullOrWhiteSpace(url))
            return;

            if (string.Equals(pr.LastDownloadedThreeMfUrl, url, StringComparison.Ordinal))
            return;

            try
            {
                var downloader = new ThreeMfPreviewDownloader("/images");
                var imagePath = downloader.DownloadAndExtractLatestPreviewAsync(url, pr.Name).GetAwaiter().GetResult();

                pr.LastDownloadedThreeMfUrl = url;

                Console.WriteLine($"[PREVIEW] printer={pr.Name} serial={pr.ID} image={imagePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREVIEW] Fehler bei printer={pr.Name} serial={pr.ID}: {ex.Message}");
            }
        }
    }
}