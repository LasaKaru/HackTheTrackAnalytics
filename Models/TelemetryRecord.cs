namespace HackTheTrackAnalytics.Models;

/// <summary>
/// Represents a single telemetry data point from the race car.
/// Supports streaming large CSV files without loading all into memory.
/// </summary>
public record TelemetryRecord
{
    public DateTime Timestamp { get; init; }
    public string VehicleId { get; init; } = string.Empty;
    public int Lap { get; init; }
    public string ParameterName { get; init; } = string.Empty;
    public double Value { get; init; }

    // Common telemetry parameters (parsed from CSV)
    public double? AccX { get; init; }  // accx_can
    public double? AccY { get; init; }  // accy_can
    public double? Throttle { get; init; }  // ath
    public double? BrakeFront { get; init; }  // pbrake_f
    public double? BrakeRear { get; init; }  // pbrake_r
    public int? Gear { get; init; }
    public double? SteeringAngle { get; init; }
    public double? Speed { get; init; }  // vCar
    public double? LapDistance { get; init; }  // Laptrigger_lapdist_dls (meters from S/F)
    public double? Latitude { get; init; }  // VBOX_Lat_Min
    public double? Longitude { get; init; }  // VBOX_Long_Min
    public string? Flag { get; init; }  // FLAG_AT_FL (e.g., "FCY" for caution)

    // Computed fields
    public double LapTime { get; init; }  // seconds
    public int CurrentSector { get; init; }  // 1, 2, or 3
}

/// <summary>
/// Compact version for in-memory caching during simulation
/// </summary>
public record CompactTelemetry(
    DateTime Timestamp,
    double LapDistance,
    double Speed,
    double Throttle,
    double BrakeFront,
    int Gear,
    double SteeringAngle,
    int Lap,
    string Flag
);
