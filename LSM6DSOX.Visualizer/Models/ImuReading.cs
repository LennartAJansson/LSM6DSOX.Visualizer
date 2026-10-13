namespace LSM6DSOX.Visualizer.Models;

/// <summary>
/// Ett sampel från LSM6DSOX, uppackat ur ESP32:ns kuvert på "sensors.lsm6dsox.raw":
/// {"type":"lsm6dsox","sender":"MAC=..,IP=..","value":"{\"t\":..,\"ax\":..,\"ay\":..,\"az\":..,\"gx\":..,\"gy\":..,\"gz\":..}"}
/// </summary>
public record ImuReading(
    DateTime Timestamp,     // Lokal tid (JetStream-tidsstämpel, annars mottagningstid)
    long DeviceTimeMs,      // ESP32:ns millisekunder sedan uppstart – används som tidssteg i orienteringsfiltret
    double AccelX,          // g
    double AccelY,
    double AccelZ,
    double GyroX,           // grader/sekund (dps)
    double GyroY,
    double GyroZ,
    string Sender);
