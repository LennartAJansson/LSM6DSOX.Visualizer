using System.Text.Json.Serialization;

namespace LSM6DSOX.Visualizer.Models;

/// <summary>
/// Rådata så som den publiceras av ESP32:n på "sensors.lsm6dsox.raw".
/// Accelerationen är i g, gyrot i grader/sekund (dps).
/// </summary>
public class ImuSample
{
    [JsonPropertyName("t")]
    public long TimestampMs { get; set; }

    [JsonPropertyName("ax")]
    public double AccelX { get; set; }

    [JsonPropertyName("ay")]
    public double AccelY { get; set; }

    [JsonPropertyName("az")]
    public double AccelZ { get; set; }

    [JsonPropertyName("gx")]
    public double GyroX { get; set; }

    [JsonPropertyName("gy")]
    public double GyroY { get; set; }

    [JsonPropertyName("gz")]
    public double GyroZ { get; set; }
}
