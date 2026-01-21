using InfluxDB.Client.Writes;
using HTW.Result;
using System.Text.Json;
using HTW.Printer;

namespace HTW.Influx.DataConverter
{
    public static class JasonToInflux
    {
        public static Result<PointData> JsonToInfluxPoint(string jsonString, PrinterDTO pr)
        {
            Dictionary<string, Object>? dict = JsonSerializer.Deserialize<Dictionary<string, Object>>(jsonString);
            if (dict?["print"] != null) dict = ((JsonElement)dict["print"]).Deserialize<Dictionary<string, Object>>();
            if (dict == null) return new Result<PointData>.Failure("Nachricht konnte nicht Serialisiert werden");
            var output = string.Join(", ", dict.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            Console.WriteLine($"JSONINFLUX: {output}");

            PointData pointData = PointData
                                    .Measurement(String.Format("Printer Data: {0}", pr.Name))
                                    .Tag("serial", pr.ID)
                                    .Tag("device", pr.Name);

            Console.WriteLine($"JSONINFLUX: {pointData}");

            foreach (var e in dict)
            {
                var (type, value) = ParseValue(e.Value.ToString()!);
                pointData = pointData.Field(e.Key, value);
            }


            return new Result<PointData>.Success(pointData);
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
