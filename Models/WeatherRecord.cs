namespace HackTheTrackAnalytics.Models;

/// <summary>
/// Weather and track conditions data
/// </summary>
public record WeatherRecord
{
    public DateTime Timestamp { get; init; }
    public double AirTemperatureCelsius { get; init; }
    public double TrackTemperatureCelsius { get; init; }
    public double Humidity { get; init; }  // Percentage
    public double WindSpeed { get; init; }  // km/h
    public string WindDirection { get; init; } = string.Empty;
    public double Pressure { get; init; }  // mBar
    public string Conditions { get; init; } = "Dry";  // Dry, Wet, Damp

    /// <summary>
    /// Calculate tire wear multiplier based on track temperature
    /// Hot track = faster tire degradation
    /// </summary>
    public double GetTireWearMultiplier()
    {
        return TrackTemperatureCelsius switch
        {
            < 25 => 0.9,      // Cool - less wear
            < 35 => 1.0,      // Normal
            < 45 => 1.2,      // Warm - more wear
            _ => 1.4          // Hot - significant wear
        };
    }

    /// <summary>
    /// Estimate grip level (affects lap times)
    /// </summary>
    public double GetGripLevel()
    {
        if (Conditions == "Wet") return 0.7;
        if (Conditions == "Damp") return 0.85;

        return TrackTemperatureCelsius switch
        {
            < 20 => 0.92,     // Cold track - less grip
            < 40 => 1.0,      // Optimal
            _ => 0.95         // Very hot - slightly less grip
        };
    }
}
