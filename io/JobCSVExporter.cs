namespace HTW.Influx.Export
{
    public static class JobCsvExporter
    {
        public static async Task moveFiles(string id, string Name)
        {
            Thread.Sleep(TimeSpan.FromMinutes(1));
            Directory.CreateDirectory($"/mnt/job-csv/{Name}/{id}/");
            try
            {
                File.Move($"/logs/{id}.csv", $"/mnt/job-csv/{Name}/{id}/{Name}_{id}.csv");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error]({DateTime.UtcNow}):MoveCSV {e}");
            }
            try
            {
                File.Move($"/logs/{id}.3mf", $"/mnt/job-csv/{Name}/{id}/{Name}_{id}.3mf");
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Error]({DateTime.UtcNow}):Move3mf {e}");
            }
        }
    }
}
