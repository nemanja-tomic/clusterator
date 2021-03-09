using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using Tkn.Clusterator.Common;
using Tkn.Clusterator.Configuration;
using Tkn.Clusterator.Interfaces;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Discovery {
	internal class UdpBroadcastHandler : IBroadcastHandler {
		readonly IMessageSender _sender;
		readonly ILogger _logger;
		readonly int _listeningTimeout;
		readonly int _broadcastPort;
		readonly IEnumerable<string> _excludedAdapters;

		bool _active;

		public UdpBroadcastHandler(IBroadcastConfig config, IMessageSender sender, ILogger logger) {
			_sender = sender;
			_logger = logger;
			_broadcastPort = config.BroadcastPort;
			_listeningTimeout = config.ListeningTimeout;
			_excludedAdapters = config.ExcludedAdapters;
		}

		async Task log(string message) {
			await _logger.Info($"UDP Broadcast Handler: {message}");
		}

		public void Subscribe(Action<AnnounceModel> handler) {
			_active = true;
			foreach (var adapter in netAdapters()) {
				log($"Subscribed to broadcast from adapter: {adapter.Name}").Wait();
				subscribeForAdapter(handler, adapter);
			}
		}

		public async Task<IEnumerable<ServiceEndpoint>> Broadcast(AnnounceModel announceModel) {
			var tasks = netAdapters().Select(adapter => publishForAdapter(announceModel, adapter)).ToList();
			await Task.WhenAll(tasks);
			return tasks.Select(x => x.Result);
		}

		public void Dispose() {
			_active = false;
		}

		async Task<ServiceEndpoint> publishForAdapter(AnnounceModel announceModel, NetworkInterface adapter) {
			try {
				var ipAddress = getIpAddress(adapter).ToString();
				announceModel.Endpoint.Host = ipAddress;
				
				await _sender.Broadcast(announceModel, ipAddress, _broadcastPort);
				return new ServiceEndpoint {
					Port = announceModel.Endpoint.Port,
					Host = ipAddress,
					MaskPrefix = string.Join(".", ipAddress.Split('.').Take(3))  //returns 192.168.15 from 192.168.15.8
				};
			} catch (Exception ex) {
				await log(ex.Message);
			}
			return null;
		}

		void subscribeForAdapter(Action<AnnounceModel> handler, NetworkInterface adapter) {
			Task.Run(() => {
				var ipAddress = getIpAddress(adapter);
				var localEndpoint = new IPEndPoint(ipAddress, _broadcastPort);

				var udpClient = new UdpClient {
					ExclusiveAddressUse = false,
					EnableBroadcast = true
				};

				var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				socket.ReceiveTimeout = _listeningTimeout * 1000;

				udpClient.Client = socket;
				udpClient.Client.Bind(localEndpoint);

				while (_active) {
					try {
						var data = udpClient.ReceiveAsync().Result;
						AnnounceModel model;
						if (!data.Buffer.TryToModel(out model))
							continue;
						handler(model);
					} catch (SocketException ex) {
						if (ex.SocketErrorCode != SocketError.TimedOut)
							throw;
					}
				}
			});
		}

		static IPAddress getIpAddress(NetworkInterface adapter) {
			foreach (var ipAddress in adapter.GetIPProperties().UnicastAddresses) {
				if (ipAddress.Address.AddressFamily == AddressFamily.InterNetwork)
					return ipAddress.Address;
			}
			return IPAddress.None;
		}

		IEnumerable<NetworkInterface> netAdapters() {
			return NetworkInterface.GetAllNetworkInterfaces()
				.Where(
					x => (x.OperationalStatus == OperationalStatus.Up)
					&& !_excludedAdapters.Any(y => x.Name.Contains(y))  //excluding adapters with name contained in _excludedAdapters field
					&& new[] {
						NetworkInterfaceType.Ethernet,
						NetworkInterfaceType.Wireless80211
					}.Contains(x.NetworkInterfaceType));
		}
	}
}