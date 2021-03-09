using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Tkn.Clusterator.Common;
using Tkn.Clusterator.Configuration;
using Tkn.Clusterator.Discovery.Events;
using Tkn.Clusterator.Interfaces;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Discovery {
	internal class DiscoveryService<T> : IDiscoveryService<T> where T : LeaderState, new() {
		Guid _id;
		bool _initialized;
		ServiceState<T> _initialState = new ServiceState<T>();

		readonly IBroadcastHandler _broadcastHandler;
		readonly IDiscoveryConfig _config;
		readonly ILogger _logger;

		public DiscoveryService(IBroadcastHandler broadcastHandler, IDiscoveryConfig config, ILogger logger) {
			_broadcastHandler = broadcastHandler;
			_config = config;
			_logger = logger;
		}

		async Task log(string message) {
			await _logger.Info($"Service Initializer: {message}");
		}

		public async Task<ServiceState<T>> Initialize() {
			_id = Guid.NewGuid();
			_broadcastHandler.Subscribe(announceHandler);
			var udpClient = new UdpClient(0, AddressFamily.InterNetwork);
			var approvementEndpoint = udpClient.Client.LocalEndPoint as IPEndPoint;
			if (approvementEndpoint == null)
				throw new Exception("Couldn't attach to a UDP port.");
			await log($"Joining --{_config.ClusterId}-- cluster, listening on {approvementEndpoint.Port}");

			listenApprovements(udpClient);
			var announceModel = new AnnounceModel {
				AnnouncerId = _id,
				Endpoint = new ServiceEndpoint {
					Port = approvementEndpoint.Port
				},
				ClusterId = _config.ClusterId
			};
			var endpoints = await _broadcastHandler.Broadcast(announceModel);
			await Task.Delay(_config.InitializeTimeout * 1000);
			if (_initialized) {
				_initialState.IsLeader = false;
				_initialState.Endpoints = endpoints;
				return _initialState;
			}

			await log("No leaders found, proclaiming myself as one.");

			var serviceEndpoints = endpoints as ServiceEndpoint[] ?? endpoints.ToArray();
			_initialized = true;
			return new ServiceState<T> {
				Id = _id,
				NodeId = _config.InitialNodeId,
				IsLeader = true,
				LeaderState = new T {
					Id = _id,
					NextNodeId = _config.InitialNodeId + 1
				},
				LeaderLeftNeighbour = new ServiceState<T>(),
				LeaderRightNeighbour = new ServiceState<T>(),
				LeaderServiceState = new ServiceState<T> {
					Id = _id,
					NodeId = _config.InitialNodeId,
					Endpoints = serviceEndpoints
				},
				Endpoints = serviceEndpoints
			};
		}

		public event HostAnnouncedHandler HostAnnounced;

		public async Task Approve(ApprovementModel<T> model, ServiceEndpoint endpoint) {
			var bytes = model.ToBytes();
			var udpClient = new UdpClient();
			await udpClient.SendAsync(
				bytes,
				bytes.Length,
				new IPEndPoint(IPAddress.Parse(endpoint.Host), endpoint.Port));
		}

		async void listenApprovements(UdpClient udpClient) {
			await Task.Run(async () => {
				while (!_initialized) {
					if (udpClient.Available == 0)
						continue;
					var data = await udpClient.ReceiveAsync();
					ApprovementModel<T> model;
					if (!data.Buffer.TryToModel(out model)) {
						await Task.Delay(200);
						continue;
					}
					setInitialValues(model);
				}
				udpClient.Dispose();
			});
		}

		void announceHandler(AnnounceModel model) {
			if ((model.AnnouncerId == _id) || (model.ClusterId != _config.ClusterId))
				return;
			OnHostAnnounced(new HostAnnouncedEventArgs(model));
		}

		void setInitialValues(ApprovementModel<T> model) {
			_initialState = new ServiceState<T> {
				Id = _id,
				NodeId = model.AssignedNodeId,
				LeaderState = model.LeaderState,
				LeaderLeftNeighbour = model.LeaderPreviousNeighbour,
				LeaderRightNeighbour = model.LeaderNextNeighbour,
				LeaderServiceState = model.LeaderServiceState
			};
			_initialized = true;
		}

		protected virtual void OnHostAnnounced(HostAnnouncedEventArgs e) {
			HostAnnounced?.Invoke(this, e);
		}
	}
}