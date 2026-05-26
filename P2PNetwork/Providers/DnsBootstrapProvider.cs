using Microsoft.Extensions.Options;
using P2PNetwork.DomainModels;
using P2PNetwork.Models;
using System.Net;
using System.Net.Sockets;

namespace P2PNetwork.Providers
{
    public class DnsBootstrapProvider
    {
        private readonly ILogger<DnsBootstrapProvider> _logger;
        private readonly HttpClient _httpClient;
        private readonly IOptionsMonitor<NetworkOptions> _options;

        public DnsBootstrapProvider(IOptionsMonitor<NetworkOptions> options, ILogger<DnsBootstrapProvider> logger, HttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
            _options = options;
        }

        // Главный метод — пытаемся найти пиров всеми способами
        public async Task<IEnumerable<PeerEndpoint>> BootstrapAsync()
        {
            var peers = new List<PeerEndpoint>();

            try
            {
                peers.AddRange(await ResolveDnsSeeds());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during DNS seed resolution");
            }

            try
            {
                peers.AddRange(await FetchPeersFromSeeds());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during HTTP seed fetching");
            }

            //var savedPeers = await LoadSavedPeers();загрузка из БД
            //peers.AddRange(savedPeers);

            return peers
                .Where(p => !p.IsBanned)
                .DistinctBy(p => p.FullAddress);
        }

        private async Task<IEnumerable<PeerEndpoint>> ResolveDnsSeeds()
        {
            var results = new List<PeerEndpoint>();
            var domains = _options.CurrentValue.DnsBootstrap.SeedDomains;

            if (domains == null)
                return results;

            foreach (var domain in domains)
            {
                try
                {
                    // Пытаемся получить IPv4 и IPv6
                    var addresses = await Dns.GetHostAddressesAsync(domain);

                    foreach (var ip in addresses)
                    {
                        // Пропускаем loopback и проблемные адреса
                        if (IPAddress.IsLoopback(ip)) continue;

                        results.Add(new PeerEndpoint
                        {
                            Id = $"dns-{Guid.NewGuid():N}",
                            Address = ip.ToString(),
                            Port = _options.CurrentValue.Port,
                            FirstSeen = DateTime.UtcNow,
                            LastSeen = DateTime.UtcNow
                        });
                    }

                    _logger.LogInformation($"DNS seed {domain} resolved to {addresses.Length} addresses");
                }
                catch (SocketException ex)
                {
                    _logger.LogWarning($"Failed to resolve DNS seed {domain}: {ex.Message}");
                    // Не фатально, продолжаем со следующего домена
                    continue;
                }
            }

            return results;
        }

        private async Task<IEnumerable<PeerEndpoint>> FetchPeersFromSeeds()
        {
            var results = new List<PeerEndpoint>();
            var fallbackSeeds = _options.CurrentValue.DnsBootstrap.FallbackSeeds;

            if (fallbackSeeds == null)
                return results;

            // Параллельно опрашиваем несколько seed-нод
            var tasks = fallbackSeeds.Select(seed => AskPeer(seed));
            var responses = await Task.WhenAll(tasks);

            foreach (var peersList in responses)
            {
                if (peersList != null)
                {
                    results.AddRange(peersList);
                }
            }

            return results;
        }

        private async Task<IEnumerable<PeerEndpoint>> AskPeer(string seedUrl)
        {
            try
            {
                var timeout = _options.CurrentValue.DnsBootstrap.TimeoutSeconds;
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

                var response = await _httpClient.GetAsync($"{seedUrl}/api/Peers/GetPeers", cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var peers = await response.Content.ReadFromJsonAsync<List<PeerEndpoint>>();
                    _logger.LogInformation($"Got {peers?.Count ?? 0} peers from {seedUrl}");
                    return peers ?? new List<PeerEndpoint>();
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning($"Timeout fetching peers from {seedUrl}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to fetch peers from {seedUrl}: {ex.Message}");
            }

            return new List<PeerEndpoint>();
        }
    }
}
