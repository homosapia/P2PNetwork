using DnsClient;
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
        private readonly ILookupClient _lookupClient;
        private readonly IOptionsMonitor<NetworkOptions> _options;

        public DnsBootstrapProvider(IOptionsMonitor<NetworkOptions> options, ILookupClient lookupClient, ILogger<DnsBootstrapProvider> logger, HttpClient httpClient)
        {
            _logger = logger;
            _lookupClient = lookupClient;
            _httpClient = httpClient;
            _options = options;
        }

        // Главный метод — пытаемся найти пиров всеми способами
        public async Task<IEnumerable<PeerEndpoint>> BootstrapAsync()
        {
            var peers = new List<PeerEndpoint>();

            // 1. DNS Seed: получаем IP-адреса и формируем из них пиров
            await SafeExecuteAsync(() => ResolveDnsSeeds(), peers, "DNS seed resolution");

            // 2. HTTP Fallback Seeds: опрашиваем seed-ноды по HTTP API
            await SafeExecuteAsync(() => FetchPeersFromSeeds(), peers, "HTTP seed fetching");

            // 3. DNS SRV Seed: опрашиваем seed-ноды по HTTP API
            await SafeExecuteAsync(() => ResolveDnsSrvSeeds(), peers, "HTTP seed fetching");

            return peers
                .Where(p => !p.IsBanned)
                .DistinctBy(p => p.FullAddress);
        }

        private async Task SafeExecuteAsync(Func<Task<IEnumerable<PeerEndpoint>>> action, List<PeerEndpoint> peers, string actionName)
        {
            try
            {
                var result = await action();
                if (result != null)
                {
                    peers.AddRange(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception during {ActionName}", actionName);
            }
        }

        // DNS Seed
        public async Task<IEnumerable<PeerEndpoint>> ResolveDnsSeeds()
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

                        string seedUrl = $"https://{ip}:{_options.CurrentValue.Port}";

                        var response = await _httpClient.GetAsync($"{seedUrl}/Api/Peers/Ping");
                        if (response.IsSuccessStatusCode)
                        {
                            var responses = await AskPeer(seedUrl);
                            results.AddRange(responses);
                        }
                        else
                        {
                            seedUrl = $"http://{ip}:{_options.CurrentValue.Port}";
                            response = await _httpClient.GetAsync($"{seedUrl}/Api/Peers/Ping");
                            if (response.IsSuccessStatusCode)
                            {
                                var responses = await AskPeer(seedUrl);
                                results.AddRange(responses);
                            }
                        }
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

        //HTTP Fallback Seeds
        public async Task<IEnumerable<PeerEndpoint>> FetchPeersFromSeeds()
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
                results.AddRange(peersList);
            }

            return results;
        }

        //DNS SRV Seed
        public async Task<IEnumerable<PeerEndpoint>> ResolveDnsSrvSeeds()
        {
            var allPeers = new List<PeerEndpoint>();
            var seedDomains = _options.CurrentValue.DnsBootstrap.SeedDomains;
            if (seedDomains == null)
                return allPeers;

            var tasks = seedDomains.Select(seed =>
            {
                var srvQuery = $"_{_options.CurrentValue.SrvName}._{_options.CurrentValue.SrvProtocol}.{seed}";
                _logger.LogInformation("Querying SRV: {Query}", srvQuery);
                return QuerySrvAsync(srvQuery);
            });
            var response = await Task.WhenAll(tasks);
            foreach (var item in response)
            {
                allPeers.AddRange(item);
            }

            return allPeers;
        }

        private async Task<IEnumerable<PeerEndpoint>> QuerySrvAsync(string srvQuery)
        {
            var peers = new List<PeerEndpoint>();

            try
            {
                // Делаем SRV-запрос
                var response = await _lookupClient.QueryAsync(srvQuery, QueryType.SRV);
                var srvRecords = response.Answers.SrvRecords();

                _logger.LogInformation("Got {Count} SRV records from {Query}",
                    srvRecords.Count(), srvQuery);

                // Для каждой SRV-записи резолвим IP
                foreach (var srv in srvRecords)
                {
                    var hostname = srv.Target.Value.TrimEnd('.');

                    var httpResponse = await _httpClient.GetAsync($"{hostname}/Api/Peers/Ping");
                    if (!httpResponse.IsSuccessStatusCode)
                        continue;

                    var peersEndpoint = await AskPeer(hostname);
                    peersEndpoint = peersEndpoint.Select(x =>
                    {
                        SrvPeerEndpoint sreModel = (SrvPeerEndpoint)x;
                        sreModel.Priority = srv.Priority;
                        sreModel.Weight = srv.Weight;
                        sreModel.Source = srvQuery;
                        return sreModel;
                    });
                    peers.AddRange(peersEndpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query SRV: {Query}", srvQuery);
            }

            return peers;
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
