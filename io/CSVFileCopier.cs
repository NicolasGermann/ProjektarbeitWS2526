
namespace HTW.IO
{
    public sealed class CSVFileCopier
    {
        private readonly string _sourceCsvPath;
        private readonly string _targetRootPath;

        public CSVFileCopier(string sourceCsvPath, string? targetRootPath = null)
        {
            if (string.IsNullOrWhiteSpace(sourceCsvPath))
                throw new ArgumentException("Der CSV-Quellpfad darf nicht leer sein.", nameof(sourceCsvPath));

            if (!File.Exists(sourceCsvPath))
                throw new FileNotFoundException("Die CSV-Datei wurde nicht gefunden.", sourceCsvPath);

            _sourceCsvPath = sourceCsvPath;

            _targetRootPath = !string.IsNullOrWhiteSpace(targetRootPath)
                ? targetRootPath
                : Environment.GetEnvironmentVariable("JOB_CSV_TARGET_ROOT")
                    ?? throw new InvalidOperationException(
                        "Kein Zielpfad gesetzt. Übergib targetRootPath oder setze JOB_CSV_TARGET_ROOT.");
        }

        public string CopyToJobFolder(string printerName, string jobId, bool overwrite = true)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("printerName darf nicht leer sein.", nameof(printerName));

            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException("jobId darf nicht leer sein.", nameof(jobId));

            EnsureTargetRootUsable(_targetRootPath);

            var targetDirectory = Path.Combine(
                _targetRootPath,
                SanitizeFileName(printerName),
                SanitizeFileName(jobId));

            Directory.CreateDirectory(targetDirectory);

            var fileName = Path.GetFileName(_sourceCsvPath);
            var targetFilePath = Path.Combine(targetDirectory, fileName);

            File.Copy(_sourceCsvPath, targetFilePath, overwrite);

            return targetFilePath;
        }

        public string CopyToJobFolderWithTimestamp(string printerName, string jobId)
        {
            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("printerName darf nicht leer sein.", nameof(printerName));

            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException("jobId darf nicht leer sein.", nameof(jobId));

            EnsureTargetRootUsable(_targetRootPath);

            var targetDirectory = Path.Combine(
                _targetRootPath,
                SanitizeFileName(printerName),
                SanitizeFileName(jobId));

            Directory.CreateDirectory(targetDirectory);

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_sourceCsvPath);
            var extension = Path.GetExtension(_sourceCsvPath);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            var targetFileName = $"{fileNameWithoutExtension}_{timestamp}{extension}";
            var targetFilePath = Path.Combine(targetDirectory, targetFileName);

            File.Copy(_sourceCsvPath, targetFilePath, overwrite: false);

            return targetFilePath;
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
