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
                        if (DateTime.UtcNow.Second == 0)
                        {
                            Console.WriteLine($"[HEARTBEAT] printer={pr.Name} serial={pr.ID} queue={pr.Messages.Count}");
                        }

                        if (pr.Messages.TryDequeue(out var msg))
                        {
                            var result = JsonToInflux.JsonToInfluxPoint(msg, pr);

                            switch (result)
                            {
                                case Result<(PointData Point, string Measurement, List<string> FieldNames, string? JobId, string? GcodeState)>.Success(var built):
                                    try
                                    {
                                        writeApi.WritePoint(built.Point, db.bucket, db.org);

                                        var effectiveJobId = built.JobId ?? pr.LastFinishedJobId;
                                        var jobLabel = string.IsNullOrWhiteSpace(effectiveJobId) ? "no-job" : effectiveJobId;

                                        Console.WriteLine(
                                            $"[INFLUX] printer={pr.Name} serial={pr.ID} job_id={jobLabel} fields=[{string.Join(", ", built.FieldNames)}]");

                                        RunWithTimeout(() => TryDownloadPreview(pr), TimeSpan.FromSeconds(30), $"Preview {pr.Name}");
                                        RunWithTimeout(() => TryCopyThreeMfToSmb(pr), TimeSpan.FromSeconds(60), $"3MF copy {pr.Name}");
                                        RunWithTimeout(() => TryExportFinishedJob(pr, db), TimeSpan.FromSeconds(60), $"CSV export {pr.Name}");
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
                var copiedPath = copier.CopyToJobFolder(pr.Name, finishedJobId);

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
                var imagePath = downloader.DownloadAndExtractLatestPreviewAsync(url, pr.Name)
                    .GetAwaiter()
                    .GetResult();

                pr.LastDownloadedThreeMfUrl = url;

                Console.WriteLine($"[PREVIEW] printer={pr.Name} serial={pr.ID} image={imagePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREVIEW] Fehler bei printer={pr.Name} serial={pr.ID}: {ex.Message}");
            }
        }

        private static void TryCopyThreeMfToSmb(PrinterDTO pr)
        {
            var url = pr.CurrentThreeMfUrl;
            var jobId = pr.CurrentJobId;

            if (string.IsNullOrWhiteSpace(url))
                return;

            if (string.IsNullOrWhiteSpace(jobId))
                return;

            var copyKey = $"{jobId}|{url}";

            if (string.Equals(pr.LastCopiedThreeMfUrl, copyKey, StringComparison.Ordinal))
                return;

            try
            {
                var copier = new ThreeMfToSmbCopier();
                var targetPath = copier
                    .CopyFromUrlToJobFolderAsync(url, pr.Name, jobId)
                    .GetAwaiter()
                    .GetResult();

                pr.LastCopiedThreeMfUrl = copyKey;

                Console.WriteLine(
                    $"[3MF-SMB] printer={pr.Name} serial={pr.ID} job_id={jobId} file={targetPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"[3MF-SMB] Fehler bei printer={pr.Name} serial={pr.ID} job_id={jobId}: {ex.Message}");
            }
        }

        private static bool RunWithTimeout(Action action, TimeSpan timeout, string label)
        {
            try
            {
                var task = Task.Run(action);

                if (!task.Wait(timeout))
                {
                    Console.WriteLine($"[TIMEOUT] {label} nach {timeout.TotalSeconds}s abgebrochen");
                    return false;
                }

                if (task.IsFaulted)
                {
                    Console.WriteLine($"[ERROR] {label}: {task.Exception?.GetBaseException()}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {label}: {ex}");
                return false;
            }
        }
    }
  
}