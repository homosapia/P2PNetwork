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

// Включаем Swagger в среде разработки
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var peerService = app.Services.GetRequiredService<PeerService>();
await peerService.StartPeerCheck();

app.MapControllers();

app.Run();
