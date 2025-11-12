using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ClosedXML.Excel;
using HackTheTrackAnalytics.Models;

namespace HackTheTrackAnalytics.Services;

/// <summary>
/// Handles streaming and parsing of large telemetry files (2GB+)
/// without loading entire dataset into memory
/// </summary>
public class DataProcessorService
{
    private readonly ILogger<DataProcessorService> _logger;

    public DataProcessorService(ILogger<DataProcessorService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Stream telemetry CSV file asynchronously (handles 2GB+ files)
    /// </summary>
    public async IAsyncEnumerable<TelemetryRecord> StreamTelemetryCsvAsync(
        Stream fileStream,
        string vehicleId = "Car1")
    {
        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        var rowCount = 0;
        var lastLap = 0;

        while (await csv.ReadAsync())
        {
            rowCount++;

            // Parse record (skip invalid rows)
            TelemetryRecord? record = null;
            try
            {
                // Parse common parameters
                var timestamp = ParseDateTime(csv.GetField("Timestamp") ?? csv.GetField("Time"));
                var lap = ParseInt(csv.GetField("Lap")) ?? lastLap;

                // Handle lap 32768 bug (timestamp continuity check)
                if (lap == 32768 || lap < 0)
                    lap = lastLap;
                else
                    lastLap = lap;

                var lapDist = ParseDouble(csv.GetField("Laptrigger_lapdist_dls"));
                var speed = ParseDouble(csv.GetField("vCar"));
                var throttle = ParseDouble(csv.GetField("ath"));
                var brakeFront = ParseDouble(csv.GetField("pbrake_f"));
                var brakeRear = ParseDouble(csv.GetField("pbrake_r"));
                var gear = ParseInt(csv.GetField("gear"));
                var steering = ParseDouble(csv.GetField("Steering_Angle"));
                var accX = ParseDouble(csv.GetField("accx_can"));
                var accY = ParseDouble(csv.GetField("accy_can"));
                var lat = ParseDouble(csv.GetField("VBOX_Lat_Min"));
                var lon = ParseDouble(csv.GetField("VBOX_Long_Min"));
                var flag = csv.GetField("FLAG_AT_FL") ?? "";

                record = new TelemetryRecord
                {
                    Timestamp = timestamp,
                    VehicleId = vehicleId,
                    Lap = lap,
                    LapDistance = lapDist ?? 0,
                    Speed = speed ?? 0,
                    Throttle = throttle ?? 0,
                    BrakeFront = brakeFront,
                    BrakeRear = brakeRear,
                    Gear = gear,
                    SteeringAngle = steering,
                    AccX = accX,
                    AccY = accY,
                    Latitude = lat,
                    Longitude = lon,
                    Flag = flag,
                    CurrentSector = DetermineSector(lapDist ?? 0)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error parsing row {Row}: {Error}", rowCount, ex.Message);
                continue; // Skip invalid rows
            }

            // Yield valid record (outside try-catch to avoid CS1626)
            if (record != null)
            {
                yield return record;

                // Progress logging every 10,000 rows
                if (rowCount % 10000 == 0)
                    _logger.LogInformation("Processed {Count} telemetry rows...", rowCount);
            }
        }

        _logger.LogInformation("Telemetry streaming complete. Total rows: {Count}", rowCount);
    }

    /// <summary>
    /// Parse lap data from results CSV
    /// </summary>
    public async Task<List<LapData>> ParseLapDataCsvAsync(Stream fileStream)
    {
        var laps = new List<LapData>();

        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            try
            {
                var lap = new LapData
                {
                    LapNumber = ParseInt(csv.GetField("Lap")) ?? 0,
                    DriverName = csv.GetField("Name") ?? "",
                    VehicleId = csv.GetField("Vehicle") ?? "",
                    LapTime = ParseTimeSpan(csv.GetField("LapTime")),
                    Sector1Time = ParseTimeSpan(csv.GetField("S1")),
                    Sector2Time = ParseTimeSpan(csv.GetField("S2")),
                    Sector3Time = ParseTimeSpan(csv.GetField("S3")),
                    Flag = csv.GetField("Flag") ?? "",
                    Position = ParseInt(csv.GetField("Pos")) ?? 0,
                    IsValid = csv.GetField("Valid")?.ToLower() != "false"
                };

                laps.Add(lap);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error parsing lap row: {Error}", ex.Message);
            }
        }

        _logger.LogInformation("Loaded {Count} laps", laps.Count);
        return laps;
    }

    /// <summary>
    /// Parse best lap benchmark data
    /// </summary>
    public async Task<BestLapBenchmark?> ParseBestLapCsvAsync(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        if (await csv.ReadAsync())
        {
            return new BestLapBenchmark
            {
                BestLapTime = ParseTimeSpan(csv.GetField("LapTime")),
                BestS1 = ParseTimeSpan(csv.GetField("S1")),
                BestS2 = ParseTimeSpan(csv.GetField("S2")),
                BestS3 = ParseTimeSpan(csv.GetField("S3")),
                DriverName = csv.GetField("Name") ?? "",
                LapNumber = ParseInt(csv.GetField("Lap")) ?? 0
            };
        }

        return null;
    }

    /// <summary>
    /// Parse Excel telemetry sample (smaller files)
    /// </summary>
    public async Task<List<TelemetryRecord>> ParseExcelTelemetryAsync(Stream fileStream, string vehicleId = "Car1")
    {
        var records = new List<TelemetryRecord>();

        await Task.Run(() =>
        {
            using var workbook = new XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);
            var rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;
            var colCount = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

            // Read header row to map columns
            var headers = new Dictionary<string, int>();
            for (int col = 1; col <= colCount; col++)
            {
                var header = worksheet.Cell(1, col).GetString();
                if (!string.IsNullOrEmpty(header))
                    headers[header] = col;
            }

            // Parse data rows
            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    var record = new TelemetryRecord
                    {
                        VehicleId = vehicleId,
                        Lap = GetIntValue(worksheet, row, headers, "Lap"),
                        Throttle = GetDoubleValue(worksheet, row, headers, "ath"),
                        BrakeFront = GetDoubleValue(worksheet, row, headers, "pbrake_f"),
                        BrakeRear = GetDoubleValue(worksheet, row, headers, "pbrake_r"),
                        Gear = GetIntValue(worksheet, row, headers, "gear"),
                        SteeringAngle = GetDoubleValue(worksheet, row, headers, "Steering_Angle"),
                        AccX = GetDoubleValue(worksheet, row, headers, "accx_can"),
                        Speed = GetDoubleValue(worksheet, row, headers, "vCar")
                    };

                    records.Add(record);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error parsing Excel row {Row}: {Error}", row, ex.Message);
                }
            }
        });

        _logger.LogInformation("Loaded {Count} records from Excel", records.Count);
        return records;
    }

    // Helper methods
    private static int DetermineSector(double lapDistance)
    {
        return lapDistance switch
        {
            < CotaTrackConfig.SectorDistances.Sector1End => 1,
            < CotaTrackConfig.SectorDistances.Sector2End => 2,
            _ => 3
        };
    }

    private static DateTime ParseDateTime(string? value)
    {
        if (string.IsNullOrEmpty(value)) return DateTime.MinValue;

        if (DateTime.TryParse(value, out var dt)) return dt;
        if (double.TryParse(value, out var seconds))
            return DateTime.Today.AddSeconds(seconds);

        return DateTime.MinValue;
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var result) ? result : null;
    }

    private static TimeSpan ParseTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TimeSpan.Zero;

        // Try parse as time (mm:ss.fff)
        if (TimeSpan.TryParse(value, out var ts)) return ts;

        // Try parse as seconds
        if (double.TryParse(value, out var seconds))
            return TimeSpan.FromSeconds(seconds);

        return TimeSpan.Zero;
    }

    private static double GetDoubleValue(IXLWorksheet ws, int row, Dictionary<string, int> headers, string key)
    {
        if (!headers.TryGetValue(key, out var col)) return 0;
        var val = ws.Cell(row, col).GetString();
        return ParseDouble(val) ?? 0;
    }

    private static int GetIntValue(IXLWorksheet ws, int row, Dictionary<string, int> headers, string key)
    {
        if (!headers.TryGetValue(key, out var col)) return 0;
        var val = ws.Cell(row, col).GetString();
        return ParseInt(val) ?? 0;
    }
}
