using HackTheTrackAnalytics.Components;
using HackTheTrackAnalytics.Services;
using HackTheTrackAnalytics.Hubs;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add MudBlazor services
builder.Services.AddMudServices();

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB for large telemetry updates
    options.EnableDetailedErrors = true;
});

// Add application services
builder.Services.AddSingleton<DataProcessorService>();
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddSingleton<SectorTimeAnalyzer>();
builder.Services.AddSingleton<PitStrategyEngine>();

// Add HTTP client for data downloads
builder.Services.AddHttpClient();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map SignalR hub
app.MapHub<RaceHub>("/racehub");

app.Run();
