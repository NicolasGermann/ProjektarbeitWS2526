using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Projektarbeit.IO
{
    public sealed class ThreeMfToSmbCopier
    {
        private readonly string _targetRootPath;

        public ThreeMfToSmbCopier(string? targetRootPath = null)
        {
            _targetRootPath = !string.IsNullOrWhiteSpace(targetRootPath)
                ? targetRootPath
                : Environment.GetEnvironmentVariable("JOB_CSV_TARGET_ROOT")
                    ?? throw new InvalidOperationException(
                        "Kein Zielpfad gesetzt. Übergib targetRootPath oder setze JOB_CSV_TARGET_ROOT.");
        }

        public async Task<string> CopyFromUrlToJobFolderAsync(
            string threeMfUrl,
            string printerName,
            string jobId,
            bool overwrite = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(threeMfUrl))
                throw new ArgumentException("threeMfUrl darf nicht leer sein.", nameof(threeMfUrl));

            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("printerName darf nicht leer sein.", nameof(printerName));

            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException("jobId darf nicht leer sein.", nameof(jobId));

            if (!Uri.TryCreate(threeMfUrl, UriKind.Absolute, out var uri))
                throw new ArgumentException("threeMfUrl ist keine gültige absolute URL.", nameof(threeMfUrl));

            EnsureTargetRootUsable(_targetRootPath);

            var targetDirectory = Path.Combine(
                _targetRootPath,
                SanitizeFileName(printerName),
                SanitizeFileName(jobId));

            Directory.CreateDirectory(targetDirectory);

            var fileName = GetSafeThreeMfFileName(uri, printerName, jobId);
            var targetFilePath = Path.Combine(targetDirectory, fileName);

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            using var response = await httpClient.GetAsync(
                uri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;

            await using var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var localFileStream = new FileStream(
                targetFilePath,
                fileMode,
                FileAccess.Write,
                FileShare.None);

            await remoteStream.CopyToAsync(localFileStream, cancellationToken);
            await localFileStream.FlushAsync(cancellationToken);

            return targetFilePath;
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
    }
}