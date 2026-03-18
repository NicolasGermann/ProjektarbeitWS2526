using InfluxDB.Client.Writes;
using HTW.Result;
using System.Globalization;
using System.Text.Json;
using HTW.Printer;

namespace HTW.Influx.DataConverter
{
    public static class JsonToInflux
    {
        public static Result<(PointData Point, string Measurement, List<string> FieldNames, string? JobId, string? GcodeState)> JsonToInfluxPoint(string jsonString, PrinterDTO pr)
        {
            try
            {
                Dictionary<string, object>? rootDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);

                if (rootDict is null)
                    return new Result<(PointData, string, List<string>, string?, string?)>.Failure("Nachricht konnte nicht serialisiert werden.");

                Dictionary<string, object>? dict = rootDict;

                if (rootDict.TryGetValue("print", out var printObj) && printObj is JsonElement printElement)
                {
                    dict = printElement.Deserialize<Dictionary<string, object>>();
                }

                if (dict is null)
                    return new Result<(PointData, string, List<string>, string?, string?)>.Failure("Print-Nachricht konnte nicht serialisiert werden.");

                UpdatePrinterStateFromMessage(dict, pr);

                string measurementName = $"Printer Data: {pr.Name}";

                var effectiveJobId = pr.CurrentJobId;

                if (string.IsNullOrWhiteSpace(effectiveJobId) && IsFinishedState(pr.CurrentGcodeState))
                    effectiveJobId = pr.LastFinishedJobId;

                PointData pointData = PointData
                    .Measurement(measurementName)
                    .Tag("serial", pr.ID)
                    .Tag("device", pr.Name);

                if (!string.IsNullOrWhiteSpace(effectiveJobId))
                    pointData = pointData.Tag("job_id", effectiveJobId);

                if (!string.IsNullOrWhiteSpace(pr.CurrentTaskId))
                    pointData = pointData.Tag("task_id", pr.CurrentTaskId!);

                if (!string.IsNullOrWhiteSpace(pr.CurrentProjectId))
                    pointData = pointData.Tag("project_id", pr.CurrentProjectId!);

                var fieldNames = new List<string>();

                foreach (var e in dict)
                {
                    if (e.Value is JsonElement jsonElement &&
                        (jsonElement.ValueKind == JsonValueKind.Object || jsonElement.ValueKind == JsonValueKind.Array))
                    {
                        continue;
                    }

                    var valueAsString = ConvertJsonValueToString(e.Value);

                    if (valueAsString is null)
                        continue;

                    var (_, value) = ParseValue(valueAsString);
                    pointData = pointData.Field(e.Key, value);
                    fieldNames.Add(e.Key);
                }

                return new Result<(PointData, string, List<string>, string?, string?)>.Success(
                    (pointData, measurementName, fieldNames, effectiveJobId, pr.CurrentGcodeState)
                );
            }
            catch (Exception ex)
            {
                return new Result<(PointData, string, List<string>, string?, string?)>.Failure($"JsonToInfluxPoint Fehler: {ex.Message}");
            }
        }

        private static void UpdatePrinterStateFromMessage(Dictionary<string, object> dict, PrinterDTO pr)
        {
            var previousJobId = pr.CurrentJobId;

            var gcodeStateRaw = TryReadString(dict, "gcode_state");
            var gcodeState = NormalizeState(gcodeStateRaw);

            var jobId = TryReadString(dict, "job_id");
            var taskId = TryReadString(dict, "task_id");
            var projectId = TryReadString(dict, "project_id");
            var subtaskName = TryReadString(dict, "subtask_name");
            var threeMfUrl = TryReadString(dict, "url");

            if (!string.IsNullOrWhiteSpace(jobId))
            {
                if (!string.IsNullOrWhiteSpace(pr.CurrentJobId) && pr.CurrentJobId != jobId)
                {
                    pr.LastFinishedJobId = pr.CurrentJobId;
                }

                pr.CurrentJobId = jobId;
            }

            if (!string.IsNullOrWhiteSpace(taskId))
                pr.CurrentTaskId = taskId;

            if (!string.IsNullOrWhiteSpace(projectId))
                pr.CurrentProjectId = projectId;

            if (!string.IsNullOrWhiteSpace(subtaskName))
                pr.CurrentSubtaskName = subtaskName;

            if (!string.IsNullOrWhiteSpace(threeMfUrl))
                pr.CurrentThreeMfUrl = threeMfUrl;

            if (!string.IsNullOrWhiteSpace(gcodeState))
                pr.CurrentGcodeState = gcodeState;

            if (!string.IsNullOrWhiteSpace(gcodeState) && IsFinishedState(gcodeState))
            {
                var finishedJobId = pr.CurrentJobId ?? previousJobId;

                if (!string.IsNullOrWhiteSpace(finishedJobId))
                    pr.LastFinishedJobId = finishedJobId;

                pr.CurrentJobId = null;
                pr.CurrentTaskId = null;
                pr.CurrentProjectId = null;
            }
        }

        private static string? NormalizeState(string? state)
        {
            if (string.IsNullOrWhiteSpace(state))
                return null;

            return state.Trim().ToUpperInvariant() switch
            {
                "FINISH" => "FINISHED",
                _ => state.Trim().ToUpperInvariant()
            };
        }

        private static bool IsFinishedState(string? state)
        {
            state = NormalizeState(state);
            return state == "FINISHED" || state == "FAILED";
        }

        private static string? TryReadString(Dictionary<string, object> dict, string key)
        {
            if (!dict.TryGetValue(key, out var value) || value is null)
                return null;

            return ConvertJsonValueToString(value);
        }

        private static string? ConvertJsonValueToString(object value)
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind switch
                {
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Number => jsonElement.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => null,
                    JsonValueKind.Array => jsonElement.ToString(),
                    JsonValueKind.Object => jsonElement.ToString(),
                    _ => jsonElement.ToString()
                };
            }

            return value.ToString();
        }

        public static (Type type, object value) ParseValue(string input)
        {
            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                return (typeof(int), i);

            if (long.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                return (typeof(long), l);

            if (double.TryParse(input, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double d))
                return (typeof(double), d);

            if (bool.TryParse(input, out bool b))
                return (typeof(bool), b);

            if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
                return (typeof(DateTime), dt);

            return (typeof(string), input);
        }
    }
}