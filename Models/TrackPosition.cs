namespace HackTheTrackAnalytics.Models;

/// <summary>
/// Represents the car's position on the track, mapped from telemetry
/// </summary>
public record TrackPosition
{
    public double LapDistanceMeters { get; init; }
    public int CurrentSector { get; init; }  // 1, 2, or 3
    public double DistanceIntoSector { get; init; }
    public double LapProgressPercent { get; init; }
    public bool IsInPitLane { get; init; }
    public (double X, double Y) PixelPosition { get; init; }  // For canvas rendering
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double Speed { get; init; }  // km/h
    public DateTime Timestamp { get; init; }

    // Track location context
    public int? NearestTurn { get; init; }  // 1-20
    public bool IsAtSpeedTrap { get; init; }
    public string TrackZone { get; init; } = string.Empty;  // "Main Straight", "Turn 1", etc.
}

/// <summary>
/// Maps lap distance to pixel coordinates on the SVG track map
/// </summary>
public static class TrackMapper
{
    // SVG viewBox dimensions
    public const int ViewBoxWidth = 1200;
    public const int ViewBoxHeight = 800;

    /// <summary>
    /// Converts Laptrigger_lapdist_dls (meters from S/F) to track position
    /// </summary>
    public static TrackPosition GetTrackPosition(double lapDistMeters, double speed, DateTime timestamp)
    {
        // Normalize distance to handle lap wrapping and fix lap 32768 bug
        var normalized = lapDistMeters % CotaTrackConfig.CircuitLengthMeters;
        if (normalized < 0) normalized += CotaTrackConfig.CircuitLengthMeters;

        // Determine current sector
        var sector = normalized switch
        {
            < CotaTrackConfig.SectorDistances.Sector1End => 1,
            < CotaTrackConfig.SectorDistances.Sector2End => 2,
            _ => 3
        };

        var distanceIntoSector = sector switch
        {
            1 => normalized,
            2 => normalized - CotaTrackConfig.SectorDistances.Sector1End,
            _ => normalized - CotaTrackConfig.SectorDistances.Sector2End
        };

        var progressPercent = (normalized / CotaTrackConfig.CircuitLengthMeters) * 100.0;

        // Check if in pit lane
        var isInPits = normalized >= CotaTrackConfig.SectorDistances.PitInFromSF_Meters &&
                      normalized <= (CotaTrackConfig.SectorDistances.PitInFromSF_Meters +
                                    CotaTrackConfig.SectorDistances.PitInToPitOut_Meters + 100);

        // Check if at speed trap (ST is at 3.407m from S/F)
        var isAtSpeedTrap = Math.Abs(normalized - CotaTrackConfig.SectorDistances.SpeedTrap_ST_Meters) < 50;

        // Map to pixel position (approximate - will be refined with actual SVG path)
        var pixelPos = MapDistanceToPixel(normalized);

        return new TrackPosition
        {
            LapDistanceMeters = normalized,
            CurrentSector = sector,
            DistanceIntoSector = distanceIntoSector,
            LapProgressPercent = progressPercent,
            IsInPitLane = isInPits,
            PixelPosition = pixelPos,
            Speed = speed,
            Timestamp = timestamp,
            IsAtSpeedTrap = isAtSpeedTrap
        };
    }

    /// <summary>
    /// Approximates pixel position on track (simplified - actual implementation uses SVG path)
    /// </summary>
    private static (double X, double Y) MapDistanceToPixel(double distanceMeters)
    {
        // Simplified circular approximation for now
        // TODO: Replace with actual SVG path coordinates
        var angle = (distanceMeters / CotaTrackConfig.CircuitLengthMeters) * 2 * Math.PI;
        var centerX = ViewBoxWidth / 2.0;
        var centerY = ViewBoxHeight / 2.0;
        var radius = Math.Min(centerX, centerY) * 0.7;

        var x = centerX + radius * Math.Cos(angle - Math.PI / 2);
        var y = centerY + radius * Math.Sin(angle - Math.PI / 2);

        return (x, y);
    }
}
