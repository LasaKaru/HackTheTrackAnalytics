using HackTheTrackAnalytics.Models;

namespace HackTheTrackAnalytics.Services;

/// <summary>
/// Analyzes sector times and calculates deltas vs. best laps
/// </summary>
public class SectorTimeAnalyzer
{
    private readonly ILogger<SectorTimeAnalyzer> _logger;
    private readonly Dictionary<string, SectorTimingState> _timingStates = new();

    public SectorTimeAnalyzer(ILogger<SectorTimeAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Update timing state with new telemetry point
    /// </summary>
    public List<SectorDelta>? UpdateTiming(
        TelemetryRecord telemetry,
        BestLapBenchmark? benchmark)
    {
        var key = $"{telemetry.VehicleId}_{telemetry.Lap}";

        if (!_timingStates.TryGetValue(key, out var state))
        {
            state = new SectorTimingState
            {
                VehicleId = telemetry.VehicleId,
                LapNumber = telemetry.Lap,
                LapStartTime = telemetry.Timestamp
            };
            _timingStates[key] = state;
        }

        var currentSector = DetermineSector(telemetry.LapDistance ?? 0);
        var prevSector = state.CurrentSector;

        // Detect sector crossing
        if (currentSector != prevSector && prevSector > 0)
        {
            var sectorTime = telemetry.Timestamp - state.LastSectorTime;

            switch (prevSector)
            {
                case 1:
                    state.Sector1Time = sectorTime;
                    break;
                case 2:
                    state.Sector2Time = sectorTime;
                    break;
                case 3:
                    state.Sector3Time = sectorTime;
                    break;
            }

            state.LastSectorTime = telemetry.Timestamp;

            _logger.LogDebug(
                "Sector {Sector} complete: {Time:F3}s (Lap {Lap})",
                prevSector, sectorTime.TotalSeconds, telemetry.Lap);
        }

        if (prevSector == 0)
        {
            state.LastSectorTime = telemetry.Timestamp;
        }

        state.CurrentSector = currentSector;

        // Calculate deltas if we have completed sectors and a benchmark
        if (benchmark != null)
        {
            return CalculateDeltas(state, benchmark);
        }

        return null;
    }

    /// <summary>
    /// Calculate sector deltas vs. best lap
    /// </summary>
    private List<SectorDelta> CalculateDeltas(SectorTimingState state, BestLapBenchmark benchmark)
    {
        var deltas = new List<SectorDelta>();

        if (state.Sector1Time > TimeSpan.Zero)
        {
            var delta = state.Sector1Time - benchmark.BestS1;
            deltas.Add(new SectorDelta
            {
                Sector = 1,
                CurrentTime = state.Sector1Time,
                BestTime = benchmark.BestS1,
                Delta = delta,
                Status = GetSectorStatus(delta)
            });
        }

        if (state.Sector2Time > TimeSpan.Zero)
        {
            var delta = state.Sector2Time - benchmark.BestS2;
            deltas.Add(new SectorDelta
            {
                Sector = 2,
                CurrentTime = state.Sector2Time,
                BestTime = benchmark.BestS2,
                Delta = delta,
                Status = GetSectorStatus(delta)
            });
        }

        if (state.Sector3Time > TimeSpan.Zero)
        {
            var delta = state.Sector3Time - benchmark.BestS3;
            deltas.Add(new SectorDelta
            {
                Sector = 3,
                CurrentTime = state.Sector3Time,
                BestTime = benchmark.BestS3,
                Delta = delta,
                Status = GetSectorStatus(delta)
            });
        }

        return deltas;
    }

    /// <summary>
    /// Determine sector status based on delta
    /// </summary>
    private static SectorStatus GetSectorStatus(TimeSpan delta)
    {
        return delta.TotalSeconds switch
        {
            < 0 => SectorStatus.Green,        // Faster than best!
            < 1.0 => SectorStatus.Yellow,     // Within 1 second
            _ => SectorStatus.Red             // More than 1 second slower
        };
    }

    private static int DetermineSector(double lapDistance)
    {
        return lapDistance switch
        {
            < CotaTrackConfig.SectorDistances.Sector1End => 1,
            < CotaTrackConfig.SectorDistances.Sector2End => 2,
            _ => 3
        };
    }

    /// <summary>
    /// Get lap time for completed lap
    /// </summary>
    public TimeSpan? GetLapTime(string vehicleId, int lapNumber)
    {
        var key = $"{vehicleId}_{lapNumber}";
        if (_timingStates.TryGetValue(key, out var state))
        {
            if (state.Sector1Time > TimeSpan.Zero &&
                state.Sector2Time > TimeSpan.Zero &&
                state.Sector3Time > TimeSpan.Zero)
            {
                return state.Sector1Time + state.Sector2Time + state.Sector3Time;
            }
        }
        return null;
    }

    /// <summary>
    /// Clear old timing states to prevent memory buildup
    /// </summary>
    public void ClearOldStates(int keepLastNLaps = 5)
    {
        var toRemove = _timingStates
            .OrderByDescending(kvp => kvp.Value.LapNumber)
            .Skip(keepLastNLaps)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in toRemove)
            _timingStates.Remove(key);
    }
}

/// <summary>
/// Tracks timing state for a single lap
/// </summary>
public class SectorTimingState
{
    public string VehicleId { get; set; } = string.Empty;
    public int LapNumber { get; set; }
    public DateTime LapStartTime { get; set; }
    public DateTime LastSectorTime { get; set; }
    public int CurrentSector { get; set; }
    public TimeSpan Sector1Time { get; set; }
    public TimeSpan Sector2Time { get; set; }
    public TimeSpan Sector3Time { get; set; }
}
