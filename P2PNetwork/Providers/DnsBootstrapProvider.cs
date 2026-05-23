using P2PNetwork.DomainModels;
using System.Net;
using System.Net.Sockets;

namespace P2PNetwork.Providers
{
    public class DnsBootstrapProvider
    {
        private readonly IConfiguration _config;
        private readonly ILogger<DnsBootstrapProvider> _logger;
        private readonly HttpClient _httpClient;

        public DnsBootstrapProvider(IConfiguration config, ILogger<DnsBootstrapProvider> logger, HttpClient httpClient)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClient;
        }

        // Главный метод — пытаемся найти пиров всеми способами
        public async Task<IEnumerable<PeerEndpoint>> BootstrapAsync()
        {
            var peers = new List<PeerEndpoint>();

            var dnsPeers = await ResolveDnsSeeds();
            peers.AddRange(dnsPeers);

            var httpPeers = await FetchPeersFromSeeds();
            peers.AddRange(httpPeers);

            //var savedPeers = await LoadSavedPeers();загрузка из БД
            //peers.AddRange(savedPeers);

            return peers
                .Where(p => !p.IsBanned)
                .DistinctBy(p => p.FullAddress);
        }

        private async Task<List<PeerEndpoint>> ResolveDnsSeeds()
        {
            var results = new List<PeerEndpoint>();
            var domains = _config.GetSection("Network:DnsBootstrap:SeedDomains").Get<string[]>();

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
                            Port = _config.GetValue<int>("Network:Port"),
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

        private async Task<List<PeerEndpoint>> FetchPeersFromSeeds()
        {
            var results = new List<PeerEndpoint>();
            var fallbackSeeds = _config.GetSection("Network:DnsBootstrap:FallbackSeeds").Get<string[]>();

            if (fallbackSeeds == null)
                return results;

            // Параллельно опрашиваем несколько seed-нод
            var tasks = fallbackSeeds.Select(seed => FetchPeersFromSeed(seed));
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

        private async Task<List<PeerEndpoint>> FetchPeersFromSeed(string seedUrl)
        {
            try
            {
                var timeout = _config.GetValue<int>("Network:DnsBootstrap:TimeoutSeconds");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

                var response = await _httpClient.GetAsync($"{seedUrl}/api/peers", cts.Token);

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
