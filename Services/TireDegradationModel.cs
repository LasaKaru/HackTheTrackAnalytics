using HackTheTrackAnalytics.Models;

namespace HackTheTrackAnalytics.Services;

/// <summary>
/// Tire Degradation Model - Predicts tire wear based on multiple factors
/// Formula: Wear = f(BrakePressure, Laps, TrackTemp, LapTimeDelta)
/// </summary>
public class TireDegradationModel
{
    private readonly ILogger<TireDegradationModel> _logger;

    public TireDegradationModel(ILogger<TireDegradationModel> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate tire degradation for current stint
    /// </summary>
    public TireDegradation CalculateDegradation(
        int currentLap,
        List<LapData> completedLaps,
        List<TelemetryRecord> recentTelemetry,
        BestLapBenchmark benchmark,
        double trackTemperature)
    {
        if (!completedLaps.Any())
        {
            return new TireDegradation
            {
                CurrentLap = currentLap,
                WearPercent = 0,
                LapTimeDelta = TimeSpan.Zero
            };
        }

        // Factor 1: Lap count (base wear)
        var baseLapWear = CalculateBaseLapWear(currentLap);

        // Factor 2: Brake pressure (high braking = more wear)
        var brakePressure = CalculateAverageBrakePressure(recentTelemetry);
        var brakeWearFactor = CalculateBrakeWearFactor(brakePressure);

        // Factor 3: Lap time degradation (slower laps = worn tires)
        var lapTimeDelta = CalculateLapTimeDelta(completedLaps, benchmark);
        var timeWearFactor = CalculateTimeWearFactor(lapTimeDelta);

        // Factor 4: Track temperature effect
        var tempMultiplier = CalculateTemperatureMultiplier(trackTemperature);

        // Combined wear calculation
        var totalWear = (baseLapWear + brakeWearFactor + timeWearFactor) * tempMultiplier;
        totalWear = Math.Min(100, Math.Max(0, totalWear));

        var peakBrake = recentTelemetry
            .Where(t => t.BrakeFront.HasValue)
            .Select(t => t.BrakeFront!.Value)
            .DefaultIfEmpty(0)
            .Max();

        _logger.LogDebug(
            "Tire degradation: {Wear:F1}% (Lap {Lap}, Brake {Brake:F1}, Delta {Delta:F2}s, Temp {Temp}°C)",
            totalWear, currentLap, brakePressure, lapTimeDelta.TotalSeconds, trackTemperature);

        return new TireDegradation
        {
            CurrentLap = currentLap,
            WearPercent = totalWear,
            LapTimeDelta = lapTimeDelta,
            AverageBrakePressure = brakePressure,
            PeakBrakePressure = peakBrake,
            TemperatureEffect = tempMultiplier
        };
    }

    /// <summary>
    /// Base wear from lap count (assumes 30-lap stint)
    /// </summary>
    private double CalculateBaseLapWear(int currentLap)
    {
        const int MaxStintLaps = 30;
        return (currentLap / (double)MaxStintLaps) * 100.0;
    }

    /// <summary>
    /// Calculate average brake pressure from recent telemetry
    /// </summary>
    private double CalculateAverageBrakePressure(List<TelemetryRecord> telemetry)
    {
        if (!telemetry.Any()) return 0;

        var brakeRecords = telemetry
            .Where(t => t.BrakeFront.HasValue)
            .Select(t => t.BrakeFront!.Value)
            .ToList();

        return brakeRecords.Any() ? brakeRecords.Average() : 0;
    }

    /// <summary>
    /// Convert brake pressure to wear factor
    /// High braking zones (e.g., COTA T1, T11, T12) increase wear
    /// </summary>
    private double CalculateBrakeWearFactor(double avgBrakePressure)
    {
        // Typical brake pressure: 50-100 bar
        // Wear factor: 0-20%
        var normalizedBrake = avgBrakePressure / 100.0;
        return normalizedBrake * 20.0;
    }

    /// <summary>
    /// Calculate lap time delta vs. best lap
    /// </summary>
    private TimeSpan CalculateLapTimeDelta(List<LapData> laps, BestLapBenchmark benchmark)
    {
        var recentLaps = laps.TakeLast(3).ToList();
        if (!recentLaps.Any()) return TimeSpan.Zero;

        var avgRecentTime = TimeSpan.FromSeconds(
            recentLaps.Average(l => l.LapTime.TotalSeconds)
        );

        return avgRecentTime - benchmark.BestLapTime;
    }

    /// <summary>
    /// Convert lap time delta to wear factor
    /// +3 seconds = 100% worn (extreme case)
    /// </summary>
    private double CalculateTimeWearFactor(TimeSpan delta)
    {
        const double MaxDeltaSeconds = 3.0;
        var normalizedDelta = Math.Max(0, delta.TotalSeconds) / MaxDeltaSeconds;
        return normalizedDelta * 100.0;
    }

    /// <summary>
    /// Track temperature multiplier
    /// Hot track = faster tire degradation
    /// </summary>
    private double CalculateTemperatureMultiplier(double trackTemp)
    {
        return trackTemp switch
        {
            < 20 => 0.85,  // Cold - tires last longer
            < 30 => 0.95,  // Cool - slightly longer
            < 40 => 1.0,   // Optimal - normal wear
            < 50 => 1.3,   // Hot - faster wear
            _ => 1.5       // Very hot - rapid degradation
        };
    }

    /// <summary>
    /// Predict remaining laps before pit required
    /// </summary>
    public int PredictLapsRemaining(TireDegradation degradation, double wearThreshold = 85.0)
    {
        if (degradation.WearPercent >= wearThreshold)
            return 0;

        var wearPerLap = degradation.WearPercent / degradation.CurrentLap;
        var remainingWear = wearThreshold - degradation.WearPercent;

        return (int)Math.Ceiling(remainingWear / wearPerLap);
    }

    /// <summary>
    /// Estimate lap time improvement with fresh tires
    /// </summary>
    public TimeSpan EstimateFreshTireGain(TireDegradation degradation)
    {
        // Assume fresh tires recover 80% of lost time
        return TimeSpan.FromSeconds(degradation.LapTimeDelta.TotalSeconds * 0.8);
    }
}
