using Microsoft.AspNetCore.SignalR;
using HackTheTrackAnalytics.Models;

namespace HackTheTrackAnalytics.Hubs;

/// <summary>
/// SignalR hub for real-time race telemetry and analytics updates
/// Pushes live data to connected clients during simulation
/// </summary>
public class RaceHub : Hub
{
    private readonly ILogger<RaceHub> _logger;

    public RaceHub(ILogger<RaceHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Client joins a specific simulation session
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);
        _logger.LogInformation("Client {ConnectionId} joined session {SessionId}",
            Context.ConnectionId, sessionId);
    }

    /// <summary>
    /// Client leaves a simulation session
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
        _logger.LogInformation("Client {ConnectionId} left session {SessionId}",
            Context.ConnectionId, sessionId);
    }

    // Server-side methods to push updates (called from SimulationEngine)

    /// <summary>
    /// Push telemetry update to all clients in session
    /// </summary>
    public async Task BroadcastTelemetryUpdate(
        string sessionId,
        TelemetryRecord telemetry,
        TrackPosition position)
    {
        await Clients.Group(sessionId).SendAsync("ReceiveTelemetryUpdate", new
        {
            sessionId,
            telemetry,
            position,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Push sector crossing event
    /// </summary>
    public async Task BroadcastSectorCrossing(
        string sessionId,
        int sector,
        TimeSpan sectorTime,
        List<SectorDelta>? deltas)
    {
        await Clients.Group(sessionId).SendAsync("ReceiveSectorCrossing", new
        {
            sessionId,
            sector,
            sectorTime = sectorTime.TotalSeconds,
            deltas
        });
    }

    /// <summary>
    /// Push lap completed event
    /// </summary>
    public async Task BroadcastLapCompleted(
        string sessionId,
        int lapNumber,
        TimeSpan lapTime,
        List<SectorDelta> deltas)
    {
        await Clients.Group(sessionId).SendAsync("ReceiveLapCompleted", new
        {
            sessionId,
            lapNumber,
            lapTime = lapTime.TotalSeconds,
            deltas
        });
    }

    /// <summary>
    /// Push pit recommendation
    /// </summary>
    public async Task BroadcastPitRecommendation(
        string sessionId,
        PitRecommendation recommendation)
    {
        await Clients.Group(sessionId).SendAsync("ReceivePitRecommendation", recommendation);
    }

    /// <summary>
    /// Push caution flag alert
    /// </summary>
    public async Task BroadcastCautionFlag(
        string sessionId,
        string flagType,
        int lapNumber,
        TrackPosition position)
    {
        await Clients.Group(sessionId).SendAsync("ReceiveCautionFlag", new
        {
            sessionId,
            flagType,
            lapNumber,
            position
        });
    }

    /// <summary>
    /// Push simulation status update
    /// </summary>
    public async Task BroadcastSimulationStatus(
        string sessionId,
        bool isRunning,
        double speedMultiplier,
        double progressPercent)
    {
        await Clients.Group(sessionId).SendAsync("ReceiveSimulationStatus", new
        {
            sessionId,
            isRunning,
            speedMultiplier,
            progressPercent
        });
    }
}
