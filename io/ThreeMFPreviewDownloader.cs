using System.IO.Compression;

namespace HTW.Images
{
    public sealed class ThreeMfPreviewDownloader
    {
        public static async Task DownloadThreeMF(string jobID, string printerName, string url)
        {
            try
            {
                using var client = new HttpClient();
                byte[] fileBytes = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync($"/logs/{jobID}.3mf", fileBytes);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error]({DateTime.UtcNow}:3MF {e})");
            }
            try
            {

                string zipPath = $"/logs/{jobID}.3mf";
                string internalPath = "Metadata/plate_1.png";
                string exportPath = $"/images/{printerName}.png";

                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    // GetEntry sucht exakt nach dem String
                    ZipArchiveEntry entry = archive.GetEntry(internalPath)!;

                    if (entry != null)
                    {
                        // Sicherstellen, dass das lokale Zielverzeichnis existiert
                        string destinationDir = Path.GetDirectoryName(exportPath)!;
                        if (!string.IsNullOrEmpty(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }

                        // Datei extrahieren
                        entry.ExtractToFile(exportPath, overwrite: true);
                        Console.WriteLine("Datei erfolgreich extrahiert.");
                    }
                    else
                    {
                        Console.WriteLine("Die Datei wurde im Archiv nicht gefunden. Prüfe die Pfadschreibweise!");
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error]({DateTime.UtcNow}:ZIP {e})");
            }
        }
    }
}
