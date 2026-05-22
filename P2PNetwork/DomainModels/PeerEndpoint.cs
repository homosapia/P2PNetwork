namespace P2PNetwork.DomainModels
{
    public class PeerEndpoint
    {
        public string Id { get; set; }
        public string Address { get; set; }
        public int Port { get; set; }
        public DateTime LastSeen { get; set; }
        public DateTime FirstSeen { get; set; }
        public int FailedAttempts { get; set; }
        public bool IsBanned => FailedAttempts >= 5;

        public string FullAddress => $"{Address}:{Port}";
        public string HttpUrl => $"http://{FullAddress}";
        public string HttpsUrl => $"https://{FullAddress}";
    }
}
