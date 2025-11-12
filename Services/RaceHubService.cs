using Microsoft.AspNetCore.SignalR;
using HackTheTrackAnalytics.Models;
using HackTheTrackAnalytics.Hubs;

namespace HackTheTrackAnalytics.Services;

/// <summary>
/// Service interface for SignalR race updates
/// Decouples business logic from SignalR Hub implementation
/// </summary>
public interface IRaceHubService
{
    Task BroadcastPositionUpdate(string sessionId, TelemetryRecord telemetry, TrackPosition position);
    Task BroadcastSectorCrossing(string sessionId, int sector, TimeSpan sectorTime, List<SectorDelta>? deltas);
    Task BroadcastLapCompleted(string sessionId, int lapNumber, TimeSpan lapTime, List<SectorDelta> deltas);
    Task BroadcastPitRecommendation(string sessionId, PitRecommendation recommendation);
    Task BroadcastCautionFlag(string sessionId, string flagType, int lapNumber, TrackPosition position);
    Task BroadcastSimulationStatus(string sessionId, bool isRunning, double speedMultiplier, double progressPercent);
    Task BroadcastTelemetryUpdate(string sessionId, double speed, double brake, double throttle, int gear);
    Task BroadcastLeaderboardUpdate(string sessionId, List<object> standings);
}

/// <summary>
/// Implementation of IRaceHubService using SignalR
/// </summary>
public class RaceHubService : IRaceHubService
{
    private readonly IHubContext<RaceHub> _hubContext;
    private readonly ILogger<RaceHubService> _logger;

    public RaceHubService(
        IHubContext<RaceHub> hubContext,
        ILogger<RaceHubService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast position update to all clients in session
    /// </summary>
    public async Task BroadcastPositionUpdate(
        string sessionId,
        TelemetryRecord telemetry,
        TrackPosition position)
    {
        try
        {
            await _hubContext.Clients
                .Group(GetGroupName(sessionId))
                .SendAsync("ReceiveTelemetryUpdate", new
                {
                    sessionId,
                    telemetry,
                    position,
                    timestamp = DateTime.UtcNow
                });

            _logger.LogTrace(
                "Position update broadcasted: Lap {Lap}, Sector {Sector}, {Progress:F1}%",
                telemetry.Lap, position.CurrentSector, position.LapProgressPercent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting position update");
        }
    }

    /// <summary>
    /// Broadcast sector crossing event
    /// </summary>
    public async Task BroadcastSectorCrossing(
        string sessionId,
        int sector,
        TimeSpan sectorTime,
        List<SectorDelta>? deltas)
    {
        try
        {
            await _hubContext.Clients
                .Group(GetGroupName(sessionId))
                .SendAsync("ReceiveSectorCrossing", new
                {
                    sessionId,
                    sector,
                    sectorTime = sectorTime.TotalSeconds,
                    deltas,
                    timestamp = DateTime.UtcNow
                });

            _logger.LogInformation(
                "Sector {Sector} crossed: {Time:F3}s",
                sector, sectorTime.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting sector crossing");
        }
    }

    /// <summary>
    /// Broadcast lap completion
    /// </summary>
    public async Task BroadcastLapCompleted(
        string sessionId,
        int lapNumber,
        TimeSpan lapTime,
        List<SectorDelta> deltas)
    {
        try
        {
            await _hubContext.Clients
                .Group(GetGroupName(sessionId))
                .SendAsync("ReceiveLapCompleted", new
                {
                    sessionId,
                    lapNumber,
                    lapTime = lapTime.TotalSeconds,
                    deltas,
                    timestamp = DateTime.UtcNow
                });

            _logger.LogInformation(
                "Lap {Lap} completed: {Time:F3}s",
                lapNumber, lapTime.TotalSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting lap completed");
        }
    }

    /// <summary>
    /// Broadcast pit strategy recommendation
    /// </summary>
    public async Task BroadcastPitRecommendation(
        string sessionId,
        PitRecommendation recommendation)
    {
        try
        {
            await _hubContext.Clients
                .Group(GetGroupName(sessionId))
                .SendAsync("ReceivePitRecommendation", recommendation);

            _logger.LogInformation(
                "Pit recommendation: {Message} (Urgency: {Urgency})",
                recommendation.GetDisplayMessage(), recommendation.Urgency);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting pit recommendation");
        }
    }

    /// <summary>
    /// Broadcast caution flag
    /// </summary>
    public async Task BroadcastCautionFlag(
        string sessionId,
        string flagType,
        int lapNumber,
        TrackPosition position)
    {
        try
        {
            await _hubContext.Clients
                .Group(GetGroupName(sessionId))
                .SendAsync("ReceiveCautionFlag", new
                {
                    sessionId,
                    flagType,
                    lapNumber,
                    position,
                    timestamp = DateTime.UtcNow
                });

            _logger.LogWarning(
                "CAUTION FLAG: {FlagType} on Lap {Lap} at {Zone}",
                flagType, lapNumber, position.TrackZone);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting caution flag");
        }
    }

    /// <summary>
    /// Broadcast simulation status update
    /// </summary>
    public async Task BroadcastSimulationStatus(
        string sessionId,
        bool isRunning,
        double speedMultiplier,
        double progressPercent)
    {
        try
        {
            await _hubContext.Clients
                .Group(GetGroupName(sessionId))
                .SendAsync("ReceiveSimulationStatus", new
                {
                    sessionId,
                    isRunning,
                    speedMultiplier,
                    progressPercent,
                    timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting simulation status");
        }
    }

    /// <summary>
    /// Broadcast telemetry data for charts
    /// </summary>
    public async Task BroadcastTelemetryUpdate(
        string sessionId,
        double speed,
        double brake,
        double throttle,
        int gear)
    {
        try
        {
            await _hubContext.Clients
                .Group(GetGroupName(sessionId))
                .SendAsync("ReceiveTelemetryData", new
                {
                    sessionId,
                    speed,
                    brake,
                    throttle,
                    gear,
                    timestamp = DateTime.UtcNow
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting telemetry data");
        }
    }

    /// <summary>
    /// Broadcast leaderboard update
    /// </summary>
    public async Task BroadcastLeaderboardUpdate(
        string sessionId,
        List<object> standings)
    {
        try
        {
            await _hubContext.Clients
                .Group(GetGroupName(sessionId))
                .SendAsync("ReceiveLeaderboardUpdate", standings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting leaderboard");
        }
    }

    /// <summary>
    /// Get SignalR group name for session
    /// </summary>
    private string GetGroupName(string sessionId)
    {
        return $"simulation_{sessionId}";
    }

    /// <summary>
    /// Send message to all connected clients
    /// </summary>
    public async Task BroadcastToAll(string method, object data)
    {
        try
        {
            await _hubContext.Clients.All.SendAsync(method, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting to all clients");
        }
    }
}
