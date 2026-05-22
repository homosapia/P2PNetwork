using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using P2PNetwork.DomainModels;
using P2PNetwork.Services;

namespace P2PNetwork.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PeersController : ControllerBase
    {
        private readonly PeerService _peerManager;
        public PeersController(PeerService peerManager)
        {
            _peerManager = peerManager;
        }

        [HttpGet]
        public IActionResult GetPeers([FromQuery] int count = 20)
        {
            var peers = _peerManager.GetRandomAlivePeers(count);
            return Ok(peers);
        }

        [HttpPost("announce")]
        public IActionResult AnnouncePeer([FromBody] PeerEndpoint peer)
        {
            _peerManager.AddOrUpdatePeer(peer);
            return Ok();
        }

        [HttpGet("ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                NodeId = _peerManager.MyNodeId,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
