using HTW.Printer;

namespace HTW.IO
{
    public sealed class ThreeMfToSmbCopier
    {
        private readonly string _targetRootPath;

        private ThreeMfToSmbCopier(string? targetRootPath = null)
        {
            _targetRootPath = !string.IsNullOrWhiteSpace(targetRootPath)
                ? targetRootPath
                : Environment.GetEnvironmentVariable("JOB_CSV_TARGET_ROOT")
                    ?? throw new InvalidOperationException(
                        "Kein Zielpfad gesetzt. Übergib targetRootPath oder setze JOB_CSV_TARGET_ROOT.");
        }


        private async Task<string> CopyFromUrlToJobFolderAsync(
            string threeMfUrl,
            string printerName,
            string jobId,
            bool overwrite = true,
            CancellationToken cancellationToken = default)
        {
            EnsureTargetRootUsable(_targetRootPath);

            var targetDirectory = Path.Combine(
                _targetRootPath,
                SanitizeFileName(printerName),
                SanitizeFileName(jobId));

            Directory.CreateDirectory(targetDirectory);

            if (!Uri.TryCreate(threeMfUrl, UriKind.Absolute, out var uri))
                throw new ArgumentException("threeMfUrl ist keine gültige absolute URL.", nameof(threeMfUrl));

            var fileName = GetSafeThreeMfFileName(uri, printerName, jobId);
            var targetFilePath = Path.Combine(targetDirectory, fileName);

            var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;

            try
            {
                using var httpClient = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(30)
                };

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                using var response = await httpClient.GetAsync(
                    uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token);

                response.EnsureSuccessStatusCode();

                await using (var remoteStream = await response.Content.ReadAsStreamAsync(cts.Token))
                await using (var localFileStream = new FileStream(
                    targetFilePath,
                    fileMode,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await remoteStream.CopyToAsync(localFileStream, cts.Token);
                    await localFileStream.FlushAsync(cts.Token);
                }

                return targetFilePath;
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Download oder Schreiben der 3MF-Datei hat das Zeitlimit überschritten: {threeMfUrl}",
                    ex);
            }
        }

        private static string GetSafeThreeMfFileName(Uri uri, string printerName, string jobId)
        {
            var candidate = Path.GetFileName(uri.AbsolutePath);

            if (string.IsNullOrWhiteSpace(candidate))
                candidate = $"{SanitizeFileName(printerName)}_{SanitizeFileName(jobId)}.3mf";

            candidate = SanitizeFileName(candidate);

            if (!candidate.EndsWith(".3mf", StringComparison.OrdinalIgnoreCase))
                candidate += ".3mf";

            return candidate;
        }

        private static void EnsureTargetRootUsable(string targetRootPath)
        {
            if (!Directory.Exists(targetRootPath))
                throw new DirectoryNotFoundException(
                    $"Das Zielverzeichnis '{targetRootPath}' existiert nicht.");

            var testFilePath = Path.Combine(targetRootPath, ".write_test");

            try
            {
                File.WriteAllText(testFilePath, "test");
                File.Delete(testFilePath);
            }
            catch (Exception ex)
            {
                throw new IOException(
                    $"Das Zielverzeichnis '{targetRootPath}' ist nicht beschreibbar oder der Share ist nicht verfügbar.",
                    ex);
            }
        }

        private static string SanitizeFileName(string value)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "unknown" : cleaned;
        }


        public static void TryCopyThreeMfToSmb(PrinterDTO pr)
        {
            var url = pr.CurrentThreeMfUrl ?? throw new Exception("[3Mf] url nicht gesetzt.");
            var jobId = pr.lastJobId?.ToString() ?? throw new Exception("[3Mf] lastJobId nicht gesetzt.");
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
                    $"[Error]({DateTime.UtcNow})3MF-SMB Fehler bei printer={pr.Name} serial={pr.ID} job_id={jobId}: {ex.Message}");
            }
        }

        internal static void TryCopyThreeMfToSmb()
        {
            throw new NotImplementedException();
        }
    }

}
