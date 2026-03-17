//Copy an CSV File from tmp to an smb share thats mounted on the host

/**
try
{
    var copier = new CSVFileCopier("/tmp/export.csv");
    var result = copier.Copy();

    Console.WriteLine($"Erfolgreich kopiert: {result}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"CSV-Kopie fehlgeschlagen: {ex.Message}");
}
**/

using System;
using System.IO;

namespace Projektarbeit.IO
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

        public string Copy(bool overwrite = true)
        {
            EnsureTargetRootUsable(_targetRootPath);

            var dayFolder = DateTime.Now.ToString("yyyy-MM-dd");
            var targetDirectory = Path.Combine(_targetRootPath, dayFolder);

            Directory.CreateDirectory(targetDirectory);

            var fileName = Path.GetFileName(_sourceCsvPath);
            var targetFilePath = Path.Combine(targetDirectory, fileName);

            File.Copy(_sourceCsvPath, targetFilePath, overwrite);

            return targetFilePath;
        }

        public string CopyWithTimestamp()
        {
            EnsureTargetRootUsable(_targetRootPath);

            var dayFolder = DateTime.Now.ToString("yyyy-MM-dd");
            var targetDirectory = Path.Combine(_targetRootPath, dayFolder);

            Directory.CreateDirectory(targetDirectory);

            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_sourceCsvPath);
            var extension = Path.GetExtension(_sourceCsvPath);
            var timestamp = DateTime.Now.ToString("HHmmss");

            var targetFileName = $"{fileNameWithoutExtension}_{timestamp}{extension}";
            var targetFilePath = Path.Combine(targetDirectory, targetFileName);

            File.Copy(_sourceCsvPath, targetFilePath, false);

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
    }
}