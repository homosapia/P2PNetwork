namespace P2PNetwork.Models
{
    public class NetworkOptions
    {
        public class DnsBootstrapConfig
        {
            public string[] SeedDomains { get; set; }
            public string[] FallbackSeeds { get; set; }
            public int TimeoutSeconds { get; set; }
            public int MinPeersToStart { get; set; }
            public int MaxParallelLookups { get; set; }
        }

        public class PeerConfigFile
        {
            public string FilePath { get; set; }
            public string MaxSavedPeers { get; set; }
        }

        public string NodeId { get; set; }

        public int Port { get; set; }

        public string Protocol { get; set; }

        public DnsBootstrapConfig DnsBootstrap { get; set; }

        public PeerConfigFile PeerPersistence { get; set; }
    }
}
