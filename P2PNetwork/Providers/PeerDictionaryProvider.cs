using Microsoft.Extensions.Options;
using P2PNetwork.DomainModels;
using P2PNetwork.Models;
using System.IO;
using System.Text.Json;

namespace P2PNetwork.Providers
{
    public class PeerDictionaryProvider
    {
        private readonly IOptionsMonitor<NetworkOptions> _options;
        public PeerDictionaryProvider(IOptionsMonitor<NetworkOptions> options)
        {
            _options = options;
        }

        public async Task SavePeersAsync(IEnumerable<PeerEndpoint> peers)
        {
            var maxPeers = 200; // из конфига
            var peersToSave = peers
                .Where(p => !p.IsBanned)
                .OrderByDescending(p => p.LastSeen)
                .Take(maxPeers);

            var json = JsonSerializer.Serialize(peersToSave, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_options.CurrentValue.PeerPersistence.FilePath, json);
        }

        public async Task<IEnumerable<PeerEndpoint>> LoadPeersAsync()
        {
            if (!File.Exists(_options.CurrentValue.PeerPersistence.FilePath))
                return Enumerable.Empty<PeerEndpoint>();

            try
            {
                var json = await File.ReadAllTextAsync(_options.CurrentValue.PeerPersistence.FilePath);
                return JsonSerializer.Deserialize<List<PeerEndpoint>>(json)
                    ?? Enumerable.Empty<PeerEndpoint>();
            }
            catch
            {
                return Enumerable.Empty<PeerEndpoint>();
            }
        }
    }
}
