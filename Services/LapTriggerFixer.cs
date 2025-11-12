using HackTheTrackAnalytics.Models;

namespace HackTheTrackAnalytics.Services;

/// <summary>
/// Utility for fixing lap counter bugs (e.g., lap = 32768)
/// Uses timestamp continuity and distance-based validation
/// </summary>
public class LapTriggerFixer
{
    private readonly ILogger<LapTriggerFixer> _logger;
    private int _lastValidLap = 0;
    private DateTime? _lastTimestamp;
    private double _lastLapDistance = 0;
    private int _consecutiveInvalidLaps = 0;

    public LapTriggerFixer(ILogger<LapTriggerFixer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Fix corrupted lap counter using multiple strategies
    /// </summary>
    public int FixLapNumber(TelemetryRecord record, int reportedLap)
    {
        // Strategy 1: Check for known corruption value (32768 = 2^15)
        if (reportedLap == 32768 || reportedLap == -1 || reportedLap < 0)
        {
            _consecutiveInvalidLaps++;
            _logger.LogWarning(
                "Detected corrupted lap number: {ReportedLap} at {Timestamp}",
                reportedLap, record.Timestamp);

            return UseTimestampContinuity(record);
        }

        // Strategy 2: Check for implausible lap jumps
        if (_lastValidLap > 0 && Math.Abs(reportedLap - _lastValidLap) > 2)
        {
            _logger.LogWarning(
                "Implausible lap jump: {Last} → {Current}",
                _lastValidLap, reportedLap);

            return UseDistanceContinuity(record, reportedLap);
        }

        // Strategy 3: Validate using lap distance
        var fixedLap = ValidateWithDistance(record, reportedLap);

        _lastValidLap = fixedLap;
        _lastTimestamp = record.Timestamp;
        _lastLapDistance = record.LapDistance ?? 0;
        _consecutiveInvalidLaps = 0;

        return fixedLap;
    }

    /// <summary>
    /// Strategy 1: Use timestamp to estimate lap number
    /// Assumes ~2 min lap time at COTA
    /// </summary>
    private int UseTimestampContinuity(TelemetryRecord record)
    {
        if (_lastTimestamp == null)
        {
            _logger.LogInformation("No previous timestamp, defaulting to lap 1");
            return 1;
        }

        var timeSinceLastLap = record.Timestamp - _lastTimestamp.Value;
        const double AverageLapTimeSeconds = 125.0; // ~2:05 at COTA

        var estimatedLapIncrement = (int)(timeSinceLastLap.TotalSeconds / AverageLapTimeSeconds);
        var estimatedLap = _lastValidLap + Math.Max(0, estimatedLapIncrement);

        _logger.LogInformation(
            "Using timestamp continuity: lap {Estimated} (time delta: {Delta:F1}s)",
            estimatedLap, timeSinceLastLap.TotalSeconds);

        return estimatedLap;
    }

    /// <summary>
    /// Strategy 2: Use lap distance to detect lap crossings
    /// Lap crossing = distance wraps from ~5498m to ~0m
    /// </summary>
    private int UseDistanceContinuity(TelemetryRecord record, int reportedLap)
    {
        var currentDistance = record.LapDistance ?? 0;

        // Detect lap wrap (crossing start/finish line)
        var hasWrapped = currentDistance < 500 && _lastLapDistance > 5000;

        if (hasWrapped)
        {
            var newLap = _lastValidLap + 1;
            _logger.LogInformation(
                "Lap crossing detected via distance: {Last:F0}m → {Current:F0}m, lap {New}",
                _lastLapDistance, currentDistance, newLap);

            return newLap;
        }

        // No wrap detected, keep last valid lap
        return _lastValidLap;
    }

    /// <summary>
    /// Strategy 3: Validate lap number against distance
    /// </summary>
    private int ValidateWithDistance(TelemetryRecord record, int reportedLap)
    {
        var distance = record.LapDistance ?? 0;

        // If distance is near lap start (< 100m) and reported lap changed
        if (distance < 100 && reportedLap > _lastValidLap)
        {
            return reportedLap; // Likely valid lap increment
        }

        // If distance is mid-lap (> 1000m) and lap number changed significantly
        if (distance > 1000 && Math.Abs(reportedLap - _lastValidLap) > 1)
        {
            _logger.LogWarning(
                "Mid-lap number change detected at {Distance:F0}m, keeping lap {Last}",
                distance, _lastValidLap);

            return _lastValidLap; // Likely corruption
        }

        return reportedLap;
    }

    /// <summary>
    /// Reset state (e.g., new session)
    /// </summary>
    public void Reset()
    {
        _lastValidLap = 0;
        _lastTimestamp = null;
        _lastLapDistance = 0;
        _consecutiveInvalidLaps = 0;

        _logger.LogInformation("Lap trigger fixer reset");
    }

    /// <summary>
    /// Get diagnostic info
    /// </summary>
    public LapFixerDiagnostics GetDiagnostics()
    {
        return new LapFixerDiagnostics
        {
            LastValidLap = _lastValidLap,
            LastTimestamp = _lastTimestamp,
            ConsecutiveInvalidLaps = _consecutiveInvalidLaps,
            IsHealthy = _consecutiveInvalidLaps < 10
        };
    }
}

public record LapFixerDiagnostics
{
    public int LastValidLap { get; init; }
    public DateTime? LastTimestamp { get; init; }
    public int ConsecutiveInvalidLaps { get; init; }
    public bool IsHealthy { get; init; }
}
