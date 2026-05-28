namespace P2PNetwork.DomainModels
{
    public class SrvPeerEndpoint : PeerEndpoint
    {
        public int Priority { get; set; }
        public int Weight { get; set; }
        public string Source { get; set; }
    }
}
