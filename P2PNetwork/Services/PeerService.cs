using P2PNetwork.DomainModels;
using P2PNetwork.Providers;

namespace P2PNetwork.Services
{
    public class PeerService
    {
        private readonly DnsBootstrapProvider _dnsBootstrap;
        private readonly PeerDictionaryProvider _peerDictionary;
        public PeerService(IConfiguration configuration, DnsBootstrapProvider dnsBootstrap, PeerDictionaryProvider peerDictionary)
        {
            _dnsBootstrap = dnsBootstrap;
            _peerDictionary = peerDictionary;
            MyNodeId = configuration.GetValue<string>("Network:NodeId") ?? throw new Exception("Invalid node name");
        }

        public readonly string MyNodeId;

        public async Task<IEnumerable<PeerEndpoint>> GetRandomAlivePeers(int count)
        {
            IEnumerable<PeerEndpoint> peerEndpoints = await _peerDictionary.LoadPeersAsync();
            return peerEndpoints.Take(count);
        }

        public async Task AddOrUpdatePeer(PeerEndpoint peer)
        {
            IEnumerable<PeerEndpoint> peerEndpoints = await _peerDictionary.LoadPeersAsync();
            peerEndpoints = peerEndpoints.Union(peerEndpoints);
            await _peerDictionary.SavePeersAsync(peerEndpoints);
        }

        public async Task StartPeerCheck()
        {

        }
    }
}
