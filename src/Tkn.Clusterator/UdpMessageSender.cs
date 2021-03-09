using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Tkn.Clusterator.Configuration;
using Tkn.Clusterator.Interfaces;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator {
	public class UdpMessageSender : IMessageSender {
		readonly ILogger _logger;
		readonly IDiscoveryConfig _config;

		public UdpMessageSender(ILogger logger, IDiscoveryConfig config) {
			_logger = logger;
			_config = config;
		}

		public async Task Send(IByteConvertible model, ServiceEndpoint target) {
			model.ClusterId = _config.ClusterId;
			var bytes = model.ToBytes();
			var udpClient = new UdpClient { ExclusiveAddressUse = false };
			await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse(target.Host), target.Port));
			await log($"sent {bytes.Length} bytes to {target.Host}:{target.Port}");
		}

		public async Task Broadcast(IByteConvertible announceModel, string localIp, int broadcastPort) {
			var localEndpoint = new IPEndPoint(IPAddress.Parse(localIp), broadcastPort);

			var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
			socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
			socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

			var udpClient = new UdpClient {
				EnableBroadcast = true,
				ExclusiveAddressUse = false,
				Client = socket
			};
			udpClient.Client.Bind(localEndpoint);

			var bytes = announceModel.ToBytes();
			await udpClient.SendAsync(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, broadcastPort));
			await log($"broadcast {bytes.Length} bytes from {localIp} through port #{broadcastPort}");
		}

		async Task log(string message) {
			return;
			await _logger.Info($"UDP Message Sender: {message}");
		}
	}
}