using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using P2PNetwork.DomainModels;
using P2PNetwork.Services;
using System.Threading.Tasks;

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
        public async Task<IActionResult> GetPeers([FromQuery] int count = 20)
        {
            var peers = await _peerManager.GetRandomAlivePeers(count);
            return Ok(peers);
        }

        [HttpPost("announce")]
        public async Task<IActionResult> AnnouncePeer([FromBody] PeerEndpoint peer)
        {
            await _peerManager.AddOrUpdatePeer(peer);
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
