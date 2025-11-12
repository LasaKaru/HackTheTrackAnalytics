using HackTheTrackAnalytics.Models;
using System.Collections.Concurrent;

namespace HackTheTrackAnalytics.Services;

/// <summary>
/// Real-time simulation engine for replaying telemetry data
/// Supports variable speed playback (1x, 5x, 10x, 20x)
/// </summary>
public class SimulationEngine
{
    private readonly ILogger<SimulationEngine> _logger;
    private readonly ConcurrentDictionary<string, SimulationSession> _sessions = new();

    public SimulationEngine(ILogger<SimulationEngine> logger)
    {
        _logger = logger;
    }

    public event EventHandler<SimulationUpdateEventArgs>? OnUpdate;
    public event EventHandler<SectorCrossingEventArgs>? OnSectorCrossing;
    public event EventHandler<LapCompletedEventArgs>? OnLapCompleted;
    public event EventHandler<CautionFlagEventArgs>? OnCautionFlag;

    /// <summary>
    /// Start a new simulation session
    /// </summary>
    public async Task<string> StartSimulationAsync(
        List<TelemetryRecord> telemetryData,
        string vehicleId,
        double speedMultiplier = 1.0)
    {
        var sessionId = Guid.NewGuid().ToString();

        var session = new SimulationSession
        {
            SessionId = sessionId,
            VehicleId = vehicleId,
            TelemetryData = telemetryData.OrderBy(t => t.Timestamp).ToList(),
            SpeedMultiplier = speedMultiplier,
            IsRunning = true,
            CurrentIndex = 0
        };

        _sessions[sessionId] = session;

        _logger.LogInformation(
            "Started simulation {SessionId} with {Count} data points at {Speed}x speed",
            sessionId, telemetryData.Count, speedMultiplier);

        // Start simulation loop in background
        _ = Task.Run(() => RunSimulationAsync(session));

        return sessionId;
    }

    /// <summary>
    /// Control simulation playback
    /// </summary>
    public void Pause(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsRunning = false;
            _logger.LogInformation("Paused simulation {SessionId}", sessionId);
        }
    }

    public void Resume(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.IsRunning = true;
            _logger.LogInformation("Resumed simulation {SessionId}", sessionId);
        }
    }

    public void SetSpeed(string sessionId, double speedMultiplier)
    {
        if (_sessions.TryGetValue(sessionId, out var session))
        {
            session.SpeedMultiplier = Math.Clamp(speedMultiplier, 0.1, 20.0);
            _logger.LogInformation(
                "Changed simulation {SessionId} speed to {Speed}x",
                sessionId, session.SpeedMultiplier);
        }
    }

    public void Stop(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.IsRunning = false;
            _logger.LogInformation("Stopped simulation {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Main simulation loop
    /// </summary>
    private async Task RunSimulationAsync(SimulationSession session)
    {
        var lastUpdate = DateTime.UtcNow;
        var lastSector = 0;
        var lastLap = 0;

        while (session.IsRunning && session.CurrentIndex < session.TelemetryData.Count)
        {
            if (!session.IsRunning)
            {
                await Task.Delay(100);
                continue;
            }

            var current = session.TelemetryData[session.CurrentIndex];
            var next = session.CurrentIndex < session.TelemetryData.Count - 1
                ? session.TelemetryData[session.CurrentIndex + 1]
                : null;

            // Calculate track position
            var trackPos = TrackMapper.GetTrackPosition(
                current.LapDistance ?? 0,
                current.Speed ?? 0,
                current.Timestamp);

            // Emit update event
            OnUpdate?.Invoke(this, new SimulationUpdateEventArgs
            {
                SessionId = session.SessionId,
                VehicleId = session.VehicleId,
                Telemetry = current,
                TrackPosition = trackPos,
                CurrentIndex = session.CurrentIndex,
                TotalRecords = session.TelemetryData.Count,
                ProgressPercent = (session.CurrentIndex / (double)session.TelemetryData.Count) * 100
            });

            // Check for sector crossing
            if (trackPos.CurrentSector != lastSector)
            {
                OnSectorCrossing?.Invoke(this, new SectorCrossingEventArgs
                {
                    SessionId = session.SessionId,
                    VehicleId = session.VehicleId,
                    Sector = trackPos.CurrentSector,
                    Timestamp = current.Timestamp,
                    LapNumber = current.Lap
                });
                lastSector = trackPos.CurrentSector;
            }

            // Check for lap completion
            if (current.Lap > lastLap && lastLap > 0)
            {
                OnLapCompleted?.Invoke(this, new LapCompletedEventArgs
                {
                    SessionId = session.SessionId,
                    VehicleId = session.VehicleId,
                    LapNumber = lastLap,
                    Timestamp = current.Timestamp
                });
                lastLap = current.Lap;
            }
            else if (lastLap == 0)
            {
                lastLap = current.Lap;
            }

            // Check for caution flag
            if (!string.IsNullOrEmpty(current.Flag) && current.Flag.Contains("FCY"))
            {
                OnCautionFlag?.Invoke(this, new CautionFlagEventArgs
                {
                    SessionId = session.SessionId,
                    VehicleId = session.VehicleId,
                    FlagType = current.Flag,
                    LapNumber = current.Lap,
                    TrackPosition = trackPos
                });
            }

            // Calculate delay based on actual time delta and speed multiplier
            if (next != null)
            {
                var timeDelta = (next.Timestamp - current.Timestamp).TotalMilliseconds;
                var adjustedDelay = Math.Max(1, timeDelta / session.SpeedMultiplier);
                await Task.Delay(TimeSpan.FromMilliseconds(adjustedDelay));
            }
            else
            {
                // Default 10 Hz update rate
                await Task.Delay(100);
            }

            session.CurrentIndex++;
        }

        _logger.LogInformation("Simulation {SessionId} completed", session.SessionId);
        _sessions.TryRemove(session.SessionId, out _);
    }

    public SimulationStatus? GetStatus(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return null;

        return new SimulationStatus
        {
            SessionId = sessionId,
            IsRunning = session.IsRunning,
            CurrentIndex = session.CurrentIndex,
            TotalRecords = session.TelemetryData.Count,
            ProgressPercent = (session.CurrentIndex / (double)session.TelemetryData.Count) * 100,
            SpeedMultiplier = session.SpeedMultiplier
        };
    }
}

// Supporting classes
public class SimulationSession
{
    public string SessionId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public List<TelemetryRecord> TelemetryData { get; set; } = new();
    public double SpeedMultiplier { get; set; } = 1.0;
    public bool IsRunning { get; set; }
    public int CurrentIndex { get; set; }
}

public class SimulationUpdateEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public TelemetryRecord Telemetry { get; set; } = null!;
    public TrackPosition TrackPosition { get; set; } = null!;
    public int CurrentIndex { get; set; }
    public int TotalRecords { get; set; }
    public double ProgressPercent { get; set; }
}

public class SectorCrossingEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public int Sector { get; set; }
    public DateTime Timestamp { get; set; }
    public int LapNumber { get; set; }
}

public class LapCompletedEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public int LapNumber { get; set; }
    public DateTime Timestamp { get; set; }
}

public class CautionFlagEventArgs : EventArgs
{
    public string SessionId { get; set; } = string.Empty;
    public string VehicleId { get; set; } = string.Empty;
    public string FlagType { get; set; } = string.Empty;
    public int LapNumber { get; set; }
    public TrackPosition TrackPosition { get; set; } = null!;
}

public class SimulationStatus
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public int CurrentIndex { get; set; }
    public int TotalRecords { get; set; }
    public double ProgressPercent { get; set; }
    public double SpeedMultiplier { get; set; }
}
