using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using P2PNetwork;
using P2PNetwork.Models;
using P2PNetwork.Providers;
using P2PNetwork.Services;

ProgramInitializer.Initialization();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<NetworkOptions>(
    builder.Configuration.GetSection("Network"));

builder.Services.AddHttpClient("PeerClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddSingleton<DnsBootstrapProvider>();
builder.Services.AddSingleton<PeerCheckerProvider>();
builder.Services.AddSingleton<PeerDictionaryProvider>();
builder.Services.AddSingleton<PeerService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Lifetime.ApplicationStarted.Register(() =>
{
    var server = app.Services.GetRequiredService<IServer>();
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;

    var options = app.Services.GetRequiredService<IOptionsMonitor<NetworkOptions>>().CurrentValue;
    options.PeerPersistence.Addresses = addresses.ToList() ?? throw new Exception("Addresses not assigned");
});

app.Use(async (context, next) =>
{
    Console.WriteLine($"Запрос: {context.Request.Method} {context.Request.Path}");
    await next();
});

// Включаем Swagger в среде разработки
//if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var peerService = app.Services.GetRequiredService<PeerService>();
await peerService.StartPeerCheck();

app.MapControllers();

app.Run();
