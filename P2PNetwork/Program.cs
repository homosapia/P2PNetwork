using P2PNetwork.Models;
using P2PNetwork.Providers;
using P2PNetwork.Services;

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

var app = builder.Build();

app.MapControllers();

app.Run();
