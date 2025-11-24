using System;
using System.Text.Json.Nodes;
using InfluxDB3.Client.Write;
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
            if (dict?["print"] != null) dict =  ((JsonElement)dict["print"]).Deserialize<Dictionary<string, Object>>();
            if (dict == null) return new Result<PointData>.Failure("Nachricht konnte nicht Serialisiert werden");

            PointData pointData = PointData.Measurement(String.Format("Printer Data: {0}", pr.Name)).SetFields(dict);
            return new Result<PointData>.Success(pointData);
        }
    }
}
