//Takes an Url to an zip file and copy it to the Volue /images for the nginx server to publish it
/*
var downloader = new HTW.Images.ThreeMfPreviewDownloader();

var imagePath = await downloader.DownloadAndExtractLatestPreviewAsync(
    "https://example.org/file.3mf",
    "P1S1");

Console.WriteLine(imagePath);
*/
using System.IO.Compression;

namespace HTW.Images
{
    public sealed class ThreeMfPreviewDownloader
    {
        private readonly string _imagesRootPath;

        public ThreeMfPreviewDownloader(string imagesRootPath = "/images")
        {
            if (string.IsNullOrWhiteSpace(imagesRootPath))
                throw new ArgumentException("imagesRootPath darf nicht leer sein.", nameof(imagesRootPath));

            _imagesRootPath = imagesRootPath;
        }

        public async Task<string> DownloadAndExtractLatestPreviewAsync(
            string threeMfUrl,
            string printerName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(threeMfUrl))
                throw new ArgumentException("threeMfUrl darf nicht leer sein.", nameof(threeMfUrl));

            if (!Uri.TryCreate(threeMfUrl, UriKind.Absolute, out var uri))
                throw new ArgumentException("threeMfUrl ist keine gültige absolute URL.", nameof(threeMfUrl));

            if (string.IsNullOrWhiteSpace(printerName))
                throw new ArgumentException("printerName darf nicht leer sein.", nameof(printerName));

            Directory.CreateDirectory(_imagesRootPath);

            var safePrinterName = SanitizeFileName(printerName);
            var finalImagePath = Path.Combine(_imagesRootPath, $"{safePrinterName}.png");

            var tempRoot = Path.Combine(Path.GetTempPath(), "three-mf-preview", Guid.NewGuid().ToString("N"));
            var tempArchivePath = Path.Combine(tempRoot, "job.3mf");
            var extractDirectory = Path.Combine(tempRoot, "extract");

            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(extractDirectory);

            try
            {
                using var httpClient = new HttpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                using var response = await httpClient.GetAsync(
                    uri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cts.Token);

                response.EnsureSuccessStatusCode();

                await using var remoteStream = await response.Content.ReadAsStreamAsync(cts.Token);
                await using var localFileStream = new FileStream(
                    tempArchivePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);

                await remoteStream.CopyToAsync(localFileStream, cts.Token);
                await localFileStream.FlushAsync(cts.Token);

                ZipFile.ExtractToDirectory(tempArchivePath, extractDirectory, overwriteFiles: true);

                var extractedPlatePath = Path.Combine(extractDirectory, "Metadata", "plate_1.png");

                if (!File.Exists(extractedPlatePath))
                    throw new InvalidOperationException(
                        "In der 3MF-Datei wurde 'Metadata/plate_1.png' nicht gefunden.");

                File.Copy(extractedPlatePath, finalImagePath, overwrite: true);

                return finalImagePath;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                    // bewusst schlucken: Hauptoperation war evtl. schon erfolgreich
                }
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