using P2PNetwork.DomainModels;
using System.Collections.Concurrent;
using System.Net.Http;

namespace P2PNetwork.Providers
{
    public class PeerCheckerProvider
    {
        private readonly HttpClient _httpClient;
        public PeerCheckerProvider(HttpClient httpClient) 
        {
            _httpClient = httpClient;
        }

        public async Task<List<PeerEndpoint>> FilterAlivePeers(List<PeerEndpoint> candidates)
        {
            var alive = new ConcurrentBag<PeerEndpoint>();

            // Проверяем параллельно, но не больше 10 одновременных запросов
            await Parallel.ForEachAsync(candidates,
                new ParallelOptions { MaxDegreeOfParallelism = 10 },
                async (peer, ct) =>
                {
                    if (await PingPeer(peer))
                    {
                        peer.LastSeen = DateTime.UtcNow;
                        peer.FailedAttempts = 0;
                        alive.Add(peer);
                    }
                    else
                    {
                        peer.FailedAttempts++;
                    }
                });

            return alive.ToList();
        }

        private async Task<bool> PingPeer(PeerEndpoint peer)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = await _httpClient.GetAsync($"{peer.HttpsUrl}/api/ping", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
