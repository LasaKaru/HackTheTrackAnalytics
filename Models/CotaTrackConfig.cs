namespace HackTheTrackAnalytics.Models;

/// <summary>
/// Circuit of the Americas (COTA) track configuration
/// All measurements extracted from official track sector map
/// </summary>
public static class CotaTrackConfig
{
    // Track dimensions
    public const double CircuitLengthMiles = 3.416;
    public const double CircuitLengthMeters = 5498.3;
    public const int NumberOfTurns = 20;

    // Elevation
    public const double ElevationFinishLineFeet = 508.0;
    public const double ElevationFinishLineMeters = 154.8;

    // Pit lane
    public const double PitLaneTimeAt50Kph = 36.0;  // seconds

    /// <summary>
    /// GPS coordinates of key track points
    /// </summary>
    public static class Gps
    {
        public static readonly (double Lat, double Lon) FinishLine = (30.1343371, -97.6422583);
        public static readonly (double Lat, double Lon) PitIn = (30.1314446, -97.6340257);
        public static readonly (double Lat, double Lon) PitOut = (30.1335278, -97.6422583);
        public static readonly (double Lat, double Lon) TimingLine = (30.1335278, -97.6422583);

        // Track bounds for GPS mapping
        public const double MinLatitude = 30.1290;
        public const double MaxLatitude = 30.1370;
        public const double MinLongitude = -97.6500;
        public const double MaxLongitude = -97.6300;
    }

    /// <summary>
    /// Sector and key point distances from Start/Finish line (in meters)
    /// </summary>
    public static class SectorDistances
    {
        // Sector end points (cumulative from S/F)
        public const double Sector1End = 1308.8;   // S1 ends at 1308.8m
        public const double Sector2End = 3548.8;   // S2 ends at 3548.8m (S1 + S2)
        public const double Sector3End = 5498.3;   // S3 ends at finish (full lap)

        // Sector lengths
        public const double Sector1Length = 1308.8;
        public const double Sector2Length = 2240.0;  // 3548.8 - 1308.8
        public const double Sector3Length = 1949.5;  // 5498.3 - 3548.8

        // Key points (distances from S/F line in meters)
        public const double StartLineOffset = 3.550;      // 11.652 ft
        public const double Sector1_S1_Meters = 15.708;   // 51.528 ft
        public const double Sector2_S2_Meters = 26.886;   // 88.188 ft
        public const double Sector3_S3_Meters = 23.393;   // 76.752 ft
        public const double SpeedTrap_ST_Meters = 3.407;  // 11.176 ft - on main straight

        // Pit lane
        public const double PitInFromSF_Meters = 63.42;    // 208.088 ft
        public const double PitOutFromSFP_Meters = 4.725;  // 15.504 ft
        public const double PitInToPitOut_Meters = 6.113;  // 20.052 ft
    }

    /// <summary>
    /// Turn locations and characteristics
    /// </summary>
    public static class Turns
    {
        public record TurnInfo(int Number, string Name, double DistanceFromSF, string Type, int Gear);

        public static readonly TurnInfo[] AllTurns = new[]
        {
            new TurnInfo(1, "Turn 1", 150, "Left", 2),
            new TurnInfo(2, "Turn 2", 250, "Left", 2),
            new TurnInfo(3, "Turn 3", 450, "Right", 3),
            new TurnInfo(4, "Turn 4", 550, "Right", 3),
            new TurnInfo(5, "Turn 5", 650, "Right", 3),
            new TurnInfo(6, "Turn 6", 850, "Left", 2),
            new TurnInfo(7, "Turn 7", 950, "Left", 2),
            new TurnInfo(8, "Turn 8", 1100, "Left", 3),
            new TurnInfo(9, "Turn 9", 1200, "Left", 2),
            new TurnInfo(10, "Turn 10", 1400, "Right", 2),
            new TurnInfo(11, "Turn 11", 1700, "Right", 4),
            new TurnInfo(12, "Turn 12", 2100, "Left", 2),
            new TurnInfo(13, "Turn 13", 2300, "Left", 2),
            new TurnInfo(14, "Turn 14", 2500, "Left", 2),
            new TurnInfo(15, "Turn 15", 2900, "Left", 3),
            new TurnInfo(16, "Turn 16", 3300, "Right", 2),
            new TurnInfo(17, "Turn 17", 3500, "Right", 2),
            new TurnInfo(18, "Turn 18", 3800, "Left", 2),
            new TurnInfo(19, "Turn 19", 4200, "Left", 3),
            new TurnInfo(20, "Turn 20", 4600, "Right", 3)
        };

        public static TurnInfo? GetNearestTurn(double lapDistance)
        {
            return AllTurns
                .OrderBy(t => Math.Abs(t.DistanceFromSF - lapDistance))
                .FirstOrDefault();
        }
    }

    /// <summary>
    /// Track zones for display
    /// </summary>
    public static class Zones
    {
        public static string GetZoneName(double lapDistance)
        {
            return lapDistance switch
            {
                < 150 => "Main Straight",
                < 300 => "Turn 1-2 Complex",
                < 700 => "Esses (T3-5)",
                < 1000 => "Turn 6-7",
                < 1309 => "Turn 8-9 (End S1)",
                < 1800 => "Turn 10-11",
                < 2600 => "Turn 12-14 (Hairpins)",
                < 3549 => "Turn 15 (End S2)",
                < 3900 => "Turn 16-18",
                < 4700 => "Turn 19-20",
                _ => "Final Straight"
            };
        }
    }

    /// <summary>
    /// Typical lap time benchmarks (in seconds)
    /// </summary>
    public static class Benchmarks
    {
        public const double FastLapSeconds = 125.0;      // ~2:05
        public const double AverageLapSeconds = 130.0;   // ~2:10
        public const double SlowLapSeconds = 140.0;      // ~2:20

        // Sector times for fast lap
        public const double FastS1 = 30.0;
        public const double FastS2 = 50.0;
        public const double FastS3 = 45.0;
    }
}
