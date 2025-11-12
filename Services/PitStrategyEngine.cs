using HackTheTrackAnalytics.Models;

namespace HackTheTrackAnalytics.Services;

/// <summary>
/// AI-powered pit strategy engine
/// Analyzes tire wear, lap times, weather, and caution flags to recommend optimal pit windows
/// </summary>
public class PitStrategyEngine
{
    private readonly ILogger<PitStrategyEngine> _logger;

    public PitStrategyEngine(ILogger<PitStrategyEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generate pit recommendation based on current race conditions
    /// </summary>
    public PitRecommendation GenerateRecommendation(
        int currentLap,
        List<LapData> completedLaps,
        List<TelemetryRecord> recentTelemetry,
        BestLapBenchmark benchmark,
        WeatherRecord? weather,
        bool isCautionOut,
        int totalRaceLaps = 30)
    {
        var factors = new List<string>();

        // Calculate tire degradation
        var trackTemp = weather?.TrackTemperatureCelsius ?? 35.0;
        var tireDeg = TireDegradation.Calculate(
            currentLap,
            completedLaps,
            recentTelemetry,
            benchmark,
            trackTemp);

        factors.Add($"Tire wear: {tireDeg.WearPercent:F1}%");
        factors.Add($"Lap time delta: +{tireDeg.LapTimeDelta.TotalSeconds:F2}s");

        // RULE 1: Caution flag out - PIT NOW if in optimal window
        if (isCautionOut && currentLap > 5)
        {
            return new PitRecommendation
            {
                RecommendedLap = currentLap,
                CurrentLap = currentLap,
                Reason = "CAUTION - Pit under yellow to save time!",
                Urgency = PitUrgency.Critical,
                IsCautionOpportunity = true,
                TireWearPercent = tireDeg.WearPercent,
                Strategy = "Caution pit strategy",
                Factors = factors,
                ExpectedTimeGain = TimeSpan.FromSeconds(15),  // Save ~15s vs. green flag pit
                TrackTemperature = trackTemp
            };
        }

        // RULE 2: Critical tire wear - must pit soon
        if (tireDeg.WearPercent > 85 || tireDeg.LapTimeDelta.TotalSeconds > 2.0)
        {
            var pitLap = currentLap + 1;
            factors.Add("Critical tire degradation");

            return new PitRecommendation
            {
                RecommendedLap = pitLap,
                CurrentLap = currentLap,
                Reason = $"Tires critically worn ({tireDeg.WearPercent:F0}%), losing {tireDeg.LapTimeDelta.TotalSeconds:F1}s/lap",
                Urgency = PitUrgency.Warning,
                TireWearPercent = tireDeg.WearPercent,
                Strategy = "Emergency pit - tires degraded",
                Factors = factors,
                CurrentLapTimeDelta = tireDeg.LapTimeDelta,
                TrackTemperature = trackTemp,
                ExpectedTimeGain = TimeSpan.FromSeconds(tireDeg.LapTimeDelta.TotalSeconds * (totalRaceLaps - currentLap))
            };
        }

        // RULE 3: High wear - pit in 1-2 laps
        if (tireDeg.WearPercent > 70 || tireDeg.LapTimeDelta.TotalSeconds > 1.5)
        {
            var pitLap = currentLap + 2;
            factors.Add("High tire wear detected");

            return new PitRecommendation
            {
                RecommendedLap = pitLap,
                CurrentLap = currentLap,
                Reason = $"High tire wear ({tireDeg.WearPercent:F0}%), recommend pit in 2 laps",
                Urgency = PitUrgency.Advisory,
                TireWearPercent = tireDeg.WearPercent,
                Strategy = "Planned pit - tire management",
                Factors = factors,
                CurrentLapTimeDelta = tireDeg.LapTimeDelta,
                TrackTemperature = trackTemp,
                ExpectedTimeGain = TimeSpan.FromSeconds(5)
            };
        }

        // RULE 4: Optimal pit window (based on race length)
        var optimalPitLap = CalculateOptimalPitLap(totalRaceLaps, trackTemp);
        if (currentLap >= optimalPitLap - 2 && currentLap <= optimalPitLap + 2)
        {
            factors.Add("In optimal pit window");

            return new PitRecommendation
            {
                RecommendedLap = optimalPitLap,
                CurrentLap = currentLap,
                Reason = $"Optimal pit window (lap {optimalPitLap})",
                Urgency = PitUrgency.Advisory,
                TireWearPercent = tireDeg.WearPercent,
                Strategy = "Standard pit strategy",
                Factors = factors,
                CurrentLapTimeDelta = tireDeg.LapTimeDelta,
                TrackTemperature = trackTemp,
                ExpectedTimeGain = TimeSpan.FromSeconds(2)
            };
        }

        // RULE 5: No pit needed yet
        factors.Add("Tires in good condition");
        return new PitRecommendation
        {
            RecommendedLap = optimalPitLap,
            CurrentLap = currentLap,
            Reason = $"Tires good, plan pit around lap {optimalPitLap}",
            Urgency = PitUrgency.Info,
            TireWearPercent = tireDeg.WearPercent,
            Strategy = "Monitor and continue",
            Factors = factors,
            CurrentLapTimeDelta = tireDeg.LapTimeDelta,
            TrackTemperature = trackTemp,
            ExpectedTimeGain = TimeSpan.Zero
        };
    }

    /// <summary>
    /// Calculate optimal pit lap based on race length and conditions
    /// </summary>
    private int CalculateOptimalPitLap(int totalLaps, double trackTemp)
    {
        // Base strategy: pit at 40-50% race distance
        var basePitLap = (int)(totalLaps * 0.45);

        // Adjust for track temperature (hot = earlier pit)
        if (trackTemp > 40)
            basePitLap -= 2;
        else if (trackTemp > 45)
            basePitLap -= 3;

        return Math.Max(5, basePitLap);  // Never pit before lap 5
    }

    /// <summary>
    /// Analyze if current sector is good for pitting
    /// (e.g., pit entry in S1 at COTA is better than S3)
    /// </summary>
    public bool IsGoodSectorForPit(int sector)
    {
        // At COTA, pit entry is in S1, so pitting in S1 is optimal
        return sector == 1;
    }

    /// <summary>
    /// Estimate time lost during pit stop
    /// </summary>
    public TimeSpan EstimatePitLoss(bool isUnderCaution)
    {
        // COTA pit lane: 36 seconds at 50 kph
        var basePitTime = TimeSpan.FromSeconds(36);

        // Under caution, save time as field is slower
        if (isUnderCaution)
            return TimeSpan.FromSeconds(15);  // ~15s effective loss

        // Green flag pit: full pit lane + lost track position
        return basePitTime + TimeSpan.FromSeconds(5);  // ~41s total
    }
}
