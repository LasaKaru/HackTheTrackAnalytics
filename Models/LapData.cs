namespace HackTheTrackAnalytics.Models;

/// <summary>
/// Represents completed lap data with sector times
/// </summary>
public record LapData
{
    public int LapNumber { get; init; }
    public string VehicleId { get; init; } = string.Empty;
    public string DriverName { get; init; } = string.Empty;
    public TimeSpan LapTime { get; init; }
    public TimeSpan Sector1Time { get; init; }
    public TimeSpan Sector2Time { get; init; }
    public TimeSpan Sector3Time { get; init; }
    public string? Flag { get; init; }  // FCY, GREEN, etc.
    public bool IsValid { get; init; } = true;  // Invalid if lap deleted
    public bool IsPersonalBest { get; init; }
    public bool IsSessionBest { get; init; }
    public DateTime Timestamp { get; init; }

    // Computed metrics
    public double AverageSpeed { get; init; }  // km/h
    public int Position { get; init; }
    public TimeSpan GapToLeader { get; init; }
    public TimeSpan GapToAhead { get; init; }
}

/// <summary>
/// Best lap benchmarks for comparison
/// </summary>
public record BestLapBenchmark
{
    public TimeSpan BestLapTime { get; init; }
    public TimeSpan BestS1 { get; init; }
    public TimeSpan BestS2 { get; init; }
    public TimeSpan BestS3 { get; init; }
    public string DriverName { get; init; } = string.Empty;
    public int LapNumber { get; init; }
}

/// <summary>
/// Sector delta analysis
/// </summary>
public record SectorDelta
{
    public int Sector { get; init; }
    public TimeSpan CurrentTime { get; init; }
    public TimeSpan BestTime { get; init; }
    public TimeSpan Delta { get; init; }
    public bool IsFaster => Delta < TimeSpan.Zero;
    public SectorStatus Status { get; init; }
}

public enum SectorStatus
{
    Green,      // Faster than best
    Yellow,     // Within 1 second
    Red         // More than 1 second slower
}
