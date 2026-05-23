using P2PNetwork.DomainModels;
using P2PNetwork.Providers;
using System.Linq;
using static P2PNetwork.Models.NetworkOptions;

namespace P2PNetwork.Services
{
    public class PeerService
    {
        private readonly DnsBootstrapProvider _dnsBootstrap;
        private readonly PeerDictionaryProvider _peerDictionary;
        private readonly PeerCheckerProvider _peerChecker;
        public PeerService(IConfiguration configuration, DnsBootstrapProvider dnsBootstrap, PeerDictionaryProvider peerDictionary, PeerCheckerProvider peerChecker)
        {
            _dnsBootstrap = dnsBootstrap;
            _peerDictionary = peerDictionary;
            _peerChecker = peerChecker;
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
            var isValid = await _peerChecker.PingPeer(peer);

            if (!isValid)
                throw new Exception("Peer did not respond");

            IEnumerable<PeerEndpoint> peerEndpoints = await _peerDictionary.LoadPeersAsync();
            var dictionaryDataData = await _peerChecker.FilterAlivePeers(peerEndpoints);

            dictionaryDataData = dictionaryDataData.Union(new List<PeerEndpoint> { peer });
            await _peerDictionary.SavePeersAsync(dictionaryDataData);
        }

        public async Task StartPeerCheck()
        {
            var bootstrapData = await _dnsBootstrap.BootstrapAsync();

            var validBootstrapData = await _peerChecker.FilterAlivePeers(bootstrapData);

            var dictionaryData = await _peerDictionary.LoadPeersAsync();

            var dictionaryDataData = await _peerChecker.FilterAlivePeers(bootstrapData);

            var validPeer = validBootstrapData.Union(dictionaryDataData);

            await _peerDictionary.SavePeersAsync(validPeer);
        }
    }
}
