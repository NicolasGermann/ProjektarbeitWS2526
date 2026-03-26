using InfluxDB.Client.Writes;
using System.Text.Json;
using HTW.Printer;

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
                switch (e.Key)
                {
                    case "url":
                        pr.CurrentThreeMfUrl = Convert.ToString(value);
                        break;
                    case "subtask_id":
                        pointData.Tag("subtask_id", $"{Convert.ToString(value)}");
                        break;
                    case "job_id":
                        pointData.Tag("job_id", $"{Convert.ToString(value)}");
                        pr.lastJobId = Convert.ToString(value);
                        break;
                    case "gcode_state":
                        if ($"{Convert.ToString(value)}" != pr.gCodeState && $"{Convert.ToString(value)}" == "FINISH")
                        {
                            pr.gCodeState = $"{value}";
                            if (pr.CsvThread!.IsAlive) break;
                            pr.CsvThread!.Start();
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
