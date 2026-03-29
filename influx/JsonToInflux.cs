using InfluxDB.Client.Writes;
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
            try
            {
                if (dict?["print"] != null) dict = ((JsonElement)dict["print"]).Deserialize<Dictionary<string, Object>>();
            }
            catch
            {
                throw new Exception($"[JsonToPoint]: Opjekt konnte nicht serialisiert werden {dict!.Select(t => $"{t.Key}, {t.Value}").ToArray().ToString()}");
            }
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
                var sValue = Convert.ToString(value);
                switch (e.Key)
                {
                    case "url":
                        pr.CurrentThreeMfUrl = sValue;
                        Task.Run(async () =>
                        {
                            try
                            {
                                using var client = new HttpClient();
                                byte[] fileBytes = await client.GetByteArrayAsync(sValue);
                                await File.WriteAllBytesAsync($"/logs/{pr.lastJobId}.3mf", fileBytes);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine($"[Error]({DateTime.UtcNow}:3MF {e})");
                            }
                        }).WaitAsync(TimeSpan.FromSeconds(30));
                        break;
                    case "subtask_id":
                        pointData.Tag("subtask_id", sValue);
                        break;
                    case "job_id":
                        pointData.Tag("job_id", sValue);
                        pr.lastJobId = sValue;
                        break;
                    case "gcode_state":
                        if (sValue != pr.gCodeState && sValue == "FINISH")
                        {
                            pr.ExportThread!.Value.Item2.Append(pr.lastJobId);
                        }
                        pr.gCodeState = sValue;
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
