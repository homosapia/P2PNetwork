using Microsoft.Extensions.Options;
using P2PNetwork.DomainModels;
using P2PNetwork.Interface.Services;
using P2PNetwork.Models;
using P2PNetwork.Providers;

namespace P2PNetwork.Services
{
    public class PeerService
    {
        private readonly IDnsService _dnsBootstrap;
        private readonly PeerDictionaryProvider _peerDictionary;
        private readonly PeerCheckerProvider _peerChecker;
        private readonly IOptionsMonitor<NetworkOptions> _options;
        public PeerService(IOptionsMonitor<NetworkOptions> options, IDnsService dnsBootstrap, PeerDictionaryProvider peerDictionary, PeerCheckerProvider peerChecker)
        {
            _dnsBootstrap = dnsBootstrap;
            _peerDictionary = peerDictionary;
            _peerChecker = peerChecker;
            _options = options;
        }

        public string MyNodeId => _options.CurrentValue.NodeId;

        public async Task<IEnumerable<PeerEndpoint>> GetRandomAlivePeers(int count)
        {
            IEnumerable<PeerEndpoint> peerEndpoints = await _peerDictionary.LoadPeersAsync();
            peerEndpoints = peerEndpoints.Take(count - 1);
            List<PeerEndpoint> peers = new List<PeerEndpoint>();
            peers.AddRange(peerEndpoints);
            var addresses = _options.CurrentValue.PeerPersistence.Addresses;
            var urls = addresses.Select(x => new Uri(x));
            PeerEndpoint peer = new PeerEndpoint()
            {
                Id = MyNodeId,
                FirstSeen = _options.CurrentValue.FirstSeen,
                LastSeen = DateTime.UtcNow,
                Address = urls.First().Host,
                Port = _options.CurrentValue.Port,
                FailedAttempts = 0,
            };
            peers.Add(peer);
            return peers;
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
            var bootstrapData = await _dnsBootstrap.FindPeersOnNetwork();

            var validBootstrapData = await _peerChecker.FilterAlivePeers(bootstrapData);

            var dictionaryData = await _peerDictionary.LoadPeersAsync();

            var dictionaryDataData = await _peerChecker.FilterAlivePeers(bootstrapData);

            var validPeer = validBootstrapData.Union(dictionaryDataData);

            await _peerDictionary.SavePeersAsync(validPeer);
        }
    }
}
