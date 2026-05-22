using P2PNetwork.DomainModels;
using System.Text.Json;

namespace P2PNetwork.Providers
{
    public class PeerDictionaryProvider
    {
        private readonly string _filePath;

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

            await File.WriteAllTextAsync(_filePath, json);
        }

        public async Task<IEnumerable<PeerEndpoint>> LoadPeersAsync()
        {
            if (!File.Exists(_filePath))
                return Enumerable.Empty<PeerEndpoint>();

            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
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
