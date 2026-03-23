using InfluxDB.Client.Writes;
using HTW.IO;
using System.Text.Json;
using HTW.Printer;
using HTW.Influx.Export;

namespace HTW.Influx.DataConverter
{
    public static class JsonToInflux
    {
        public static PointData JsonToInfluxPoint(string jsonString, PrinterDTO pr)
        {
            Dictionary<string, Object>? dict = JsonSerializer.Deserialize<Dictionary<string, Object>>(jsonString);
            if (dict?["print"] != null) dict = ((JsonElement)dict["print"]).Deserialize<Dictionary<string, Object>>();
            if (dict == null) throw (new Exception("Nachricht konnte nicht Serialisiert werden"));
            var output = string.Join(", ", dict.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            var pointData = PointData
                        .Measurement(String.Format("Printer Data: {0}", pr.Name))
                        .Tag("serial", pr.ID)
                        .Tag("device", pr.Name);

            foreach (var e in dict)
            {
                var (type, value) = ParseValue(e.Value.ToString()!);
                pointData = pointData.Field(e.Key, value);
                switch (e.Key)
                {
                    case "job_id":
                        pointData.Tag("job_id", $"{(Int32)value}");
                        pr.lastJobId = (Int32)value;
                        break;
                    case "gcode_state":
                        if ($"{value}" != pr.gCodeState && $"{value}" == "FINISH")
                        {
                            var capsuleThread = new Thread(async _ =>
                            {
                                try
                                {
                                    Thread.Sleep(500);
                                    var path = await JobCsvExporter.exportCSV("my-bucket"
                                    , $"Printer Data: {pr.Name}"
                                    , pr
                                    , $"{pr.lastJobId}");

                                    var copier = new CSVFileCopier(path);

                                    var result = copier.CopyToJobFolder(pr.Name, $"{pr.lastJobId}");
                                    Console.WriteLine($"[CsvExporter] Csv Exported and copied.");

                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"[JsonToInflux] CsvExport fehlgeschlagen: {pr.lastJobId}--{e}");
                                }
                            });
                            capsuleThread.Start();
                        }
                        break;
                }
            }
            return pointData;
        }

        public static (Type type, object value) ParseValue(string input)
        {
            if (int.TryParse(input, out int i)) return (typeof(int), i);
            if (long.TryParse(input, out long l)) return (typeof(long), l);
            if (double.TryParse(input, out double d)) return (typeof(double), d);
            if (bool.TryParse(input, out bool b)) return (typeof(bool), b);
            if (DateTime.TryParse(input, out DateTime dt)) return (typeof(DateTime), dt);
            return (typeof(string), input);
        }

    }
}
