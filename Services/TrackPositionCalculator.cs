using HackTheTrackAnalytics.Models;

namespace HackTheTrackAnalytics.Services;

/// <summary>
/// Calculates track position from lap distance and GPS coordinates
/// Maps telemetry data to visual track position for real-time visualization
/// </summary>
public class TrackPositionCalculator
{
    private readonly ILogger<TrackPositionCalculator> _logger;

    // SVG canvas dimensions (from cota_track.svg)
    private const double SvgWidth = 1200;
    private const double SvgHeight = 800;

    // Approximate center for circular mapping
    private static readonly (double X, double Y) TrackCenter = (600, 400);
    private const double TrackRadius = 280;

    public TrackPositionCalculator(ILogger<TrackPositionCalculator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate track position from lap distance (primary method)
    /// </summary>
    public TrackPosition Calculate(double lapDistMeters, double? speedKph = null, DateTime? timestamp = null)
    {
        // Normalize distance to handle lap wrapping and negative values
        var normalizedDist = lapDistMeters % CotaTrackConfig.CircuitLengthMeters;
        if (normalizedDist < 0)
            normalizedDist += CotaTrackConfig.CircuitLengthMeters;

        var progressPercent = (normalizedDist / CotaTrackConfig.CircuitLengthMeters) * 100.0;

        // Determine current sector
        var sector = DetermineSector(normalizedDist);
        var distanceIntoSector = CalculateDistanceIntoSector(normalizedDist, sector);

        // Check special zones
        var isInPitLane = IsInPitLane(normalizedDist);
        var isAtSpeedTrap = IsAtSpeedTrap(normalizedDist);

        // Map to pixel coordinates
        var pixelPos = MapDistanceToPixel(normalizedDist);

        // Determine track zone name
        var trackZone = GetTrackZoneName(normalizedDist);

        // Find nearest turn
        var nearestTurn = FindNearestTurn(normalizedDist);

        return new TrackPosition
        {
            LapDistanceMeters = normalizedDist,
            CurrentSector = sector,
            DistanceIntoSector = distanceIntoSector,
            LapProgressPercent = progressPercent,
            IsInPitLane = isInPitLane,
            PixelPosition = pixelPos,
            Speed = speedKph ?? 0,
            Timestamp = timestamp ?? DateTime.UtcNow,
            IsAtSpeedTrap = isAtSpeedTrap,
            TrackZone = trackZone,
            NearestTurn = nearestTurn
        };
    }

    /// <summary>
    /// Calculate position from GPS coordinates (fallback method)
    /// </summary>
    public TrackPosition CalculateFromGPS(double latitude, double longitude, double? speedKph = null)
    {
        // Normalize GPS to lap distance (approximate)
        var lapDist = MapGPSToDistance(latitude, longitude);

        _logger.LogDebug("GPS ({Lat}, {Lon}) mapped to {Dist}m", latitude, longitude, lapDist);

        return Calculate(lapDist, speedKph);
    }

    /// <summary>
    /// Determine sector from lap distance
    /// </summary>
    private int DetermineSector(double lapDistance)
    {
        return lapDistance switch
        {
            < CotaTrackConfig.SectorDistances.Sector1End => 1,
            < CotaTrackConfig.SectorDistances.Sector2End => 2,
            _ => 3
        };
    }

    /// <summary>
    /// Calculate distance into current sector
    /// </summary>
    private double CalculateDistanceIntoSector(double lapDistance, int sector)
    {
        return sector switch
        {
            1 => lapDistance,
            2 => lapDistance - CotaTrackConfig.SectorDistances.Sector1End,
            3 => lapDistance - CotaTrackConfig.SectorDistances.Sector2End,
            _ => 0
        };
    }

    /// <summary>
    /// Check if car is in pit lane
    /// Pit In: 63.42m, Pit Out: 69.53m (63.42 + 6.113)
    /// </summary>
    private bool IsInPitLane(double lapDistance)
    {
        var pitEntryStart = CotaTrackConfig.SectorDistances.PitInFromSF_Meters;
        var pitExitEnd = pitEntryStart + CotaTrackConfig.SectorDistances.PitInToPitOut_Meters + 50;

        return lapDistance >= pitEntryStart && lapDistance <= pitExitEnd;
    }

    /// <summary>
    /// Check if car is at speed trap (ST: 3.407m from S/F)
    /// </summary>
    private bool IsAtSpeedTrap(double lapDistance)
    {
        var speedTrapLocation = CotaTrackConfig.SectorDistances.SpeedTrap_ST_Meters;
        return Math.Abs(lapDistance - speedTrapLocation) < 20.0; // ±20m tolerance
    }

    /// <summary>
    /// Map lap distance to pixel coordinates on SVG
    /// Uses circular approximation - can be refined with actual SVG path
    /// </summary>
    private (double X, double Y) MapDistanceToPixel(double lapDistance)
    {
        // Convert distance to angle (0° = start/finish at bottom)
        var angleRadians = (lapDistance / CotaTrackConfig.CircuitLengthMeters) * 2 * Math.PI;
        angleRadians -= Math.PI / 2; // Rotate so S/F is at bottom

        // Base circular position
        var x = TrackCenter.X + TrackRadius * Math.Cos(angleRadians);
        var y = TrackCenter.Y + TrackRadius * Math.Sin(angleRadians);

        // Apply track shape corrections for COTA's actual layout
        var sectorAdjustment = ApplyTrackShapeCorrections(lapDistance, x, y);

        // Clamp to canvas bounds
        x = Math.Clamp(sectorAdjustment.X, 50, SvgWidth - 50);
        y = Math.Clamp(sectorAdjustment.Y, 50, SvgHeight - 50);

        return (x, y);
    }

    /// <summary>
    /// Apply corrections for COTA's actual track shape
    /// (Simplified - full implementation would use SVG path data)
    /// </summary>
    private (double X, double Y) ApplyTrackShapeCorrections(double distance, double x, double y)
    {
        // Sector 1: Esses - shift upward and left
        if (distance < 1308.8)
        {
            x -= 50;
            y -= 80;
        }
        // Sector 2: Back straight - extend rightward
        else if (distance < 3548.8)
        {
            x += 100;
            y += 20;
        }
        // Sector 3: Hairpins and final turn - compress
        else
        {
            x -= 30;
            y += 50;
        }

        return (x, y);
    }

    /// <summary>
    /// Map GPS coordinates to lap distance (approximate)
    /// </summary>
    private double MapGPSToDistance(double latitude, double longitude)
    {
        // Use distance from finish line GPS as reference
        var finishLine = CotaTrackConfig.Gps.FinishLine;

        var latDiff = latitude - finishLine.Lat;
        var lonDiff = longitude - finishLine.Lon;

        // Rough distance calculation (Haversine would be more accurate)
        var distance = Math.Sqrt(latDiff * latDiff + lonDiff * lonDiff) * 111000; // degrees to meters

        // Map to lap distance (this is a simplification)
        var normalized = (distance / 1.5) * CotaTrackConfig.CircuitLengthMeters;

        return normalized % CotaTrackConfig.CircuitLengthMeters;
    }

    /// <summary>
    /// Get track zone name from distance
    /// </summary>
    private string GetTrackZoneName(double lapDistance)
    {
        return CotaTrackConfig.Zones.GetZoneName(lapDistance);
    }

    /// <summary>
    /// Find nearest turn number
    /// </summary>
    private int? FindNearestTurn(double lapDistance)
    {
        var nearestTurn = CotaTrackConfig.Turns.GetNearestTurn(lapDistance);
        return nearestTurn?.Number;
    }

    /// <summary>
    /// Calculate distance between two track positions
    /// </summary>
    public double CalculateDistance(TrackPosition pos1, TrackPosition pos2)
    {
        var dist = Math.Abs(pos2.LapDistanceMeters - pos1.LapDistanceMeters);

        // Handle wrap-around (e.g., position at 5490m to position at 10m)
        if (dist > CotaTrackConfig.CircuitLengthMeters / 2)
        {
            dist = CotaTrackConfig.CircuitLengthMeters - dist;
        }

        return dist;
    }

    /// <summary>
    /// Estimate time to next sector
    /// </summary>
    public TimeSpan EstimateTimeToNextSector(TrackPosition position, double averageSpeed)
    {
        if (averageSpeed <= 0) return TimeSpan.Zero;

        var nextSectorDistance = position.CurrentSector switch
        {
            1 => CotaTrackConfig.SectorDistances.Sector1End - position.LapDistanceMeters,
            2 => CotaTrackConfig.SectorDistances.Sector2End - position.LapDistanceMeters,
            3 => CotaTrackConfig.CircuitLengthMeters - position.LapDistanceMeters,
            _ => 0
        };

        var timeSeconds = nextSectorDistance / (averageSpeed / 3.6); // km/h to m/s
        return TimeSpan.FromSeconds(timeSeconds);
    }
}
