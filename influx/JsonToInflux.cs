using InfluxDB.Client.Writes;
using HTW.Result;
using System.Text.Json;
using HTW.Printer;

namespace HTW.Influx.DataConverter {
  public static class JasonToInflux {
    public static Result<(
        PointData Point, string Measurement, List<string> FieldNames,
        string? JobId, string? GcodeState)> JsonToInfluxPoint(string jsonString,
                                                              PrinterDTO pr) {
      try {
        Dictionary<string, object>? rootDict =
            JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);

        if (rootDict is null)
            return new Result<(PointData, string, List<string>, string?, string?)>.Failure("Nachricht konnte nicht serialisiert werden.");

        Dictionary<string, object>? dict = rootDict;

        if (rootDict.TryGetValue("print", out var printObj) &&
            printObj is JsonElement printElement) {
          dict = printElement.Deserialize<Dictionary<string, object>>();
        }

        if (dict is null)
            return new Result<(PointData, string, List<string>, string?, string?)>.Failure("Print-Nachricht konnte nicht serialisiert werden.");

        UpdatePrinterStateFromMessage(dict, pr);

        string measurementName = $"Printer Data: {pr.Name}";

        PointData pointData = PointData.Measurement(measurementName)
                                  .Tag("serial", pr.ID)
                                  .Tag("device", pr.Name);

        if (!string.IsNullOrWhiteSpace(pr.CurrentJobId))
          pointData = pointData.Tag("job_id", pr.CurrentJobId!);

        if (!string.IsNullOrWhiteSpace(pr.CurrentTaskId))
          pointData = pointData.Tag("task_id", pr.CurrentTaskId!);

        if (!string.IsNullOrWhiteSpace(pr.CurrentProjectId))
          pointData = pointData.Tag("project_id", pr.CurrentProjectId!);

        var fieldNames = new List<string>();

        foreach (var e in dict) {
          var valueAsString = ConvertJsonValueToString(e.Value);

          if (valueAsString is null)
            continue;

          var (_, value) = ParseValue(valueAsString);
          pointData = pointData.Field(e.Key, value);
          fieldNames.Add(e.Key);
        }

        return new Result<(PointData, string, List<string>, string?, string?)>.Success(
            (pointData, measurementName, fieldNames, pr.CurrentJobId, pr.CurrentGcodeState)
        );
      } catch (Exception ex) {
        return new Result<(PointData, string, List<string>, string?, string?)>.Failure($"JsonToInfluxPoint Fehler: {ex.Message}");
      }
    }

    private static void UpdatePrinterStateFromMessage(
        Dictionary<string, object> dict, PrinterDTO pr) {
      var gcodeState = TryReadString(dict, "gcode_state");
      if (!string.IsNullOrWhiteSpace(gcodeState)) {
        pr.CurrentGcodeState = gcodeState;

        if (IsFinishedState(gcodeState)) {
          pr.CurrentJobId = null;
          pr.CurrentTaskId = null;
          pr.CurrentProjectId = null;
        }
      }

      var jobId = TryReadString(dict, "job_id");
      if (!string.IsNullOrWhiteSpace(jobId) &&
          IsActiveState(pr.CurrentGcodeState))
        pr.CurrentJobId = jobId;

      var taskId = TryReadString(dict, "task_id");
      if (!string.IsNullOrWhiteSpace(taskId) &&
          IsActiveState(pr.CurrentGcodeState))
        pr.CurrentTaskId = taskId;

      var projectId = TryReadString(dict, "project_id");
      if (!string.IsNullOrWhiteSpace(projectId) &&
          IsActiveState(pr.CurrentGcodeState))
        pr.CurrentProjectId = projectId;

      var subtaskName = TryReadString(dict, "subtask_name");
      if (!string.IsNullOrWhiteSpace(subtaskName))
        pr.CurrentSubtaskName = subtaskName;
    }

    private static bool IsActiveState(string? state) {
      return string.Equals(state, "RUNNING",
                           StringComparison.OrdinalIgnoreCase) ||
             string.Equals(state, "PAUSED", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFinishedState(string state) {
      return string.Equals(state, "FINISHED",
                           StringComparison.OrdinalIgnoreCase) ||
             string.Equals(state, "FAILED",
                           StringComparison.OrdinalIgnoreCase) ||
             string.Equals(state, "IDLE", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadString(Dictionary<string, object> dict,
                                         string key) {
      if (!dict.TryGetValue(key, out var value) || value is null)
        return null;

      return ConvertJsonValueToString(value);
    }

    private static string? ConvertJsonValueToString(object value) {
      if (value is JsonElement jsonElement) {
        return jsonElement.ValueKind switch {
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

    public static (Type type, object value) ParseValue(string input) {
      if (int.TryParse(input, out int i))
        return (typeof(int), i);
      if (long.TryParse(input, out long l))
        return (typeof(long), l);
      if (double.TryParse(input, out double d))
        return (typeof(double), d);
      if (bool.TryParse(input, out bool b))
        return (typeof(bool), b);
      if (DateTime.TryParse(input, out DateTime dt))
        return (typeof(DateTime), dt);
      return (typeof(string), input);
    }
  }
}