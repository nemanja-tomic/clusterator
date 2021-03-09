using System;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tkn.Clusterator.Configuration;
using Tkn.Clusterator.Discovery;
using Tkn.Clusterator.Discovery.Events;
using Tkn.Clusterator.Heartbeat;
using Tkn.Clusterator.Interfaces;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator {
	internal class RingeRajaClusterator<T> : IClusterator<T> where T : LeaderState, new() {
		readonly IDiscoveryService<T> _discoveryService;
		readonly IHeartbeatService<T> _hbService;
		readonly IMessageSender _sender;

		const int SERVICE_CHECK_INTERVAL = 500; // in ms
		const int SERVICE_TIMEOUT = 1500; // in ms

		bool _initialized;
		BaseServiceState _nextNeighbour = new ServiceState<T>();
		BaseServiceState _previousNeighbour = new ServiceState<T>();
		readonly ConcurrentDictionary<Guid, HeartbeatModel<T>> _watchedServices = new ConcurrentDictionary<Guid, HeartbeatModel<T>>();
		readonly Dictionary<Guid, HeartbeatModel<T>> _suspiciousServices = new Dictionary<Guid, HeartbeatModel<T>>();

		readonly ConcurrentDictionary<Guid, int> _newServices = new ConcurrentDictionary<Guid, int>();
		Dictionary<Guid, BaseServiceState> _allHosts = new Dictionary<Guid, BaseServiceState>();
		readonly Timer _serviceCheckTimer;

		ServiceState<T> _serviceState = new ServiceState<T>();
		ClusterationJob<T> _clusterationJob;
		bool _active;
		readonly IDiscoveryConfig _discoveryConfig;
		readonly ILogger _logger;

		public RingeRajaClusterator(
			IDiscoveryService<T> discoveryService, 
			IHeartbeatService<T> hbService, 
			IMessageSender sender, 
			IDiscoveryConfig discoveryConfig, 
			ILogger logger) {
			_discoveryService = discoveryService;
			_hbService = hbService;
			_sender = sender;
			_discoveryConfig = discoveryConfig;
			_logger = logger;
			_serviceCheckTimer = new Timer(serviceCheck, null, Timeout.Infinite, SERVICE_CHECK_INTERVAL);
		}

		public async Task Clusterize(ClusterationJob<T> job) {
			if (_active)
				return;
			_active = true;
			_clusterationJob = job;
			_discoveryService.HostAnnounced += onHostAnnounced;
			await Task.WhenAll(initialize(), setupHearthbeat(), receiveNotifications(), setupServiceCheck(), executeJob());
		}

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public async Task UpdateLeaderState(T newState) {
			await Task.Run(
				() => {
					_serviceState.LeaderState = newState;
					_hbService.SenderState = _serviceState;
				});
		}

		async Task info(int message) {
			await _logger.Info(message.ToString());
		}

		async Task info(string message) {
			await _logger.Info(message);
		}

		protected virtual void Dispose(bool disposing) {
			if (!disposing)
				return;
			_discoveryService.HostAnnounced -= onHostAnnounced;
			_serviceCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
			_active = false;
		}

		async Task executeJob() {
			while (_clusterationJob.RequiresLeader && !_serviceState.IsLeader)
				await Task.Delay(1000);

			await info("Executing job...");
			await _clusterationJob.ExecutionTask(_serviceState.LeaderState);
			await info("Job finished!");
			_active = false;
		}

		async void onHostAnnounced(object sender, HostAnnouncedEventArgs e) {
			if (!_initialized || !_serviceState.IsLeader)
				return;
			var model = e.Model;
			if (model.AnnouncerId == _serviceState.Id)
				return;

			if (_newServices.TryAdd(model.AnnouncerId, _serviceState.LeaderState.NextNodeId)) {
				_serviceState.LeaderState.NextNodeId++;
				_hbService.SenderState = _serviceState;
			}

			int serviceOrderNumber;
			if (!_newServices.TryGetValue(model.AnnouncerId, out serviceOrderNumber)) {
				await info("Couldn't find new service in the concurrent dictionary.");
				return;
			}

			await info($"Leader received initialize request: {JsonConvert.SerializeObject(model)}");
			var replyModel = new ApprovementModel<T> {
				LeaderState = _serviceState.LeaderState,
				AssignedNodeId = serviceOrderNumber,
				LeaderPreviousNeighbour = _previousNeighbour,
				LeaderNextNeighbour = _nextNeighbour,
				LeaderServiceState = getSenderState()
			};
			await _discoveryService.Approve(replyModel, model.Endpoint);
			await info($"Leader replied: {JsonConvert.SerializeObject(replyModel)}");
		}

		async Task setupServiceCheck() {
			await info("Setting up service check...");
			while (!_initialized) {
				await Task.Delay(2000);
			}
			_serviceCheckTimer.Change(0, SERVICE_CHECK_INTERVAL);
			await info("Service check started!");
		}

		async void serviceCheck(object args) {
			if (!_watchedServices.Any())
				return;
			var now = DateTime.Now;
			var newlyExpired = _watchedServices.Where(
					x => ((now - x.Value.LastHeartbeat).TotalMilliseconds > SERVICE_TIMEOUT) && !_suspiciousServices.ContainsKey(x.Key))
					.ToArray();

			foreach (var expired in newlyExpired) {
				var elapsed = (now - expired.Value.LastHeartbeat).TotalMilliseconds;
				await info($"Service {expired.Value.NodeId} flagged as suspicious, {elapsed} ms elapsed since last heartbeat.");
				if (((_previousNeighbour.Id == _nextNeighbour.Id) && (expired.Key == _previousNeighbour.Id))
					|| ((_previousNeighbour.Id == Guid.Empty) && (_nextNeighbour.Id == Guid.Empty))) {
					await info("I'm the only service left :'( ... deleting neighbours...");
					_serviceState.IsLeader = true;
					_hbService.SenderState = _serviceState;
					deleteDeadNeighbour(expired.Key);
					_suspiciousServices.Clear();
					_watchedServices.Clear();
					await printNeighbours();
					break;
				}
				if (!_suspiciousServices.ContainsKey(expired.Key))
					_suspiciousServices.Add(expired.Key, expired.Value);
				await notifySuspiciousService(expired.Value, elapsed);
			}
		}

		async Task notifySuspiciousService(HeartbeatModel<T> service, double downtime) {
			var model = new SuspiciousServiceModel<T> {
				AnnouncerId = _serviceState.Id,
				SuspiciousServiceId = service.Id,
				Downtime = downtime,
				Sender = _serviceState.Id,
				HeartbeatModel = service,
				Services = new List<BaseServiceState> { getSenderState() },
				IsLeader = service.IsLeader,
				LeaderState = service.LeaderState
			};
			var tasks = new List<Task>();
			var previousEndpoint = getReachableEndpoint(_previousNeighbour.Endpoints);
			var nextEndpoint = getReachableEndpoint(_nextNeighbour.Endpoints);
			if ((_previousNeighbour.Id != Guid.Empty) && (previousEndpoint != null))
				tasks.Add(_sender.Send(model, previousEndpoint));
			if ((_nextNeighbour.Id != Guid.Empty) && (nextEndpoint != null))
				tasks.Add(_sender.Send(model, nextEndpoint));
			await Task.WhenAll(tasks);
			await info($"Suspicious service notofication sent for service {service.NodeId}");
		}

		ServiceEndpoint getReachableEndpoint(IEnumerable<ServiceEndpoint> endpoints) {
			foreach (var endpoint in endpoints) {
				var match = _serviceState.Endpoints.FirstOrDefault(x => x.MaskPrefix == endpoint.MaskPrefix);
				if (match == null)
					continue;

				return endpoint;
			}
			return null;
		}

		async Task setupHearthbeat() {
			await info("Setting up heartbeat...");
			while (!_initialized) {
				await Task.Delay(2000);
			}
			_hbService.Targets = getHeartbeatTargets();
			_hbService.SenderState = _serviceState;
			await _hbService.Start();
			await info("Heartbeat started!");
		}

		IEnumerable<ServiceEndpoint> getHeartbeatTargets() {
			return new[] {
				getReachableEndpoint(_previousNeighbour.Endpoints),
				getReachableEndpoint(_nextNeighbour.Endpoints)
			};
		}

		async Task initialize() {
			await info("Service initializing...");
			_serviceState = await _discoveryService.Initialize();
			await setInitializedValues();
			await info($"Service initialized: {_serviceState.NodeId}");
			await printNeighbours();
		}

		async Task receiveNotifications() {
			await info("Setting up notification listening...");
			while (!_initialized) {
				await Task.Delay(2000);
			}

			await Task.Run(async () => {
				await info("Notification listening started!");
				var endpoint = new IPEndPoint(IPAddress.Any, _serviceState.Endpoints.First().Port);
				using (var udpClient = new UdpClient(endpoint)) {
					while (_active) {
						if (udpClient.Available == 0) {
							await Task.Delay(50);
							continue;
						}
						var data = await udpClient.ReceiveAsync();
						var model = bytesToNotificationModel(data.Buffer);
						if (model.ClusterId != _discoveryConfig.ClusterId)
							continue;
						switch (model.NotificationType) {
							case NodeNotificationType.NeighbourUpdate:
								handleNeighbourUpdate(JsonConvert.DeserializeObject<NeighbourUpdateModel>(model.Data));
								break;
							case NodeNotificationType.Heartbeat:
								handleReceivedHeartbeat(JsonConvert.DeserializeObject<HeartbeatModel<T>>(model.Data));
								break;
							case NodeNotificationType.SuspiciousService:
								handleSuspiciousService(JsonConvert.DeserializeObject<SuspiciousServiceModel<T>>(model.Data));
								break;
							case NodeNotificationType.DeadService:
								handleDeadService(JsonConvert.DeserializeObject<DeadServiceModel<T>>(model.Data));
								break;
							case NodeNotificationType.NeighbourRegroup:
								handleNeighbourRegroup(JsonConvert.DeserializeObject<NeighbourRegroupModel<T>>(model.Data));
								break;
							case NodeNotificationType.LeaderElection:
								handleLeaderElection(JsonConvert.DeserializeObject<LeaderElectionModel<T>>(model.Data));
								break;
							default:
								throw new ArgumentOutOfRangeException(nameof(model.NotificationType));
						}
					}
				}
				await info("Notification listening STOPPED");
			});
			await info("receiveNotifications EXIT");
		}

		void handleLeaderElection(LeaderElectionModel<T> model) {
			Task.Factory.StartNew(async () => {
				if ((model.CandidateNodeId == _serviceState.NodeId) && model.Completed) {
					await info("All nodes notified who the new leader is, terminating sequence. ALL HAIL THE NEW LEADER!!!");
					_serviceState.IsLeader = true;
					_serviceState.LeaderState = model.PreviousLeaderState;
					_hbService.SenderState = _serviceState;
					return;
				}

				if (model.CandidateNodeId > _serviceState.NodeId) {
					await info($"Proposing myself as a leader => Order = {_serviceState.NodeId}, Model Order = {model.CandidateNodeId}.");
					model.CandidateNodeId = _serviceState.NodeId;
					model.CandidateId = _serviceState.Id;
					model.Completed = false;
				} else if (model.CandidateNodeId == _serviceState.NodeId) {
					await info($"Elected {_serviceState.NodeId} as new leader. Announcing it to the rest of the nodes.");
					model.Completed = true;
				} else
					await info($"Voting for {model.CandidateNodeId}.");

				if (model.Completed && _watchedServices.ContainsKey(model.CandidateId)) {
					await info("Voting is complete, setting new leader parameters.");
					_watchedServices[model.CandidateId].IsLeader = true;
					_watchedServices[model.CandidateId].LeaderState = model.PreviousLeaderState;
				}

				await _sender.Send(model, getReachableEndpoint(_previousNeighbour.Endpoints));
				await info($"Service [{_serviceState.NodeId}] has finished a voting round => voted for {model.CandidateNodeId}, sent to {_previousNeighbour.NodeId}");
			});
		}

		void handleNeighbourRegroup(NeighbourRegroupModel<T> model) {
			Task.Factory.StartNew(async () => {
				if (model.Announcer == _serviceState.Id) {
					await info("Regroup finished!");
					if (model.LeaderDown) {
						await info("Initiating leader election.");
						await notifyLeaderElection(model);
					}
					return;
				}
				await info($"Regrouping neighbours for service [{_serviceState.NodeId}]");
				foreach (var service in model.AllServices.Where(x => x.Id != _serviceState.Id)) {
					addHost(service);
				}
				setNeighbours();
				setWatchOnNeighbours();
				await printNeighbours();
				model.Sender = _serviceState.Id;
				await _sender.Send(model, getReachableEndpoint(_nextNeighbour.Endpoints));
			});
		}

		async Task notifyLeaderElection(NeighbourRegroupModel<T> regroupModel) {
			var model = new LeaderElectionModel<T> {
				CandidateNodeId = _serviceState.NodeId,
				CandidateId = _serviceState.Id,
				PreviousLeaderState = regroupModel.LeaderState
			};
			await _sender.Send(model, getReachableEndpoint(_previousNeighbour.Endpoints));
			await info($"Initiated leader election, initial vote: {model.CandidateNodeId}, sent it to {_previousNeighbour.NodeId}");
		}

		void handleDeadService(DeadServiceModel<T> model) {
			Task.Factory.StartNew(async () => {
				//TODO: perform neighbour update with discovered nodes
				if (model.AnnouncerId == _serviceState.Id) {
					await info("Dead service notification made the full circle.");
					foreach (var serviceState in model.Services) {
						await info($"Service {serviceState.NodeId} exists.");
					}
					deleteDeadNeighbour(model.DeadServiceId);
					updateNeighbours(model);
					await notifyNeighbourRegroup(model);
					await info($"Neighbour regroup sent from service [{_serviceState.NodeId}]");
					return;
				}
				await info($"Dead service notification received for service [{model.DeadServiceId}]");
				BaseServiceState destinationService;
				var myIndex = model.Services.FindIndex(x => x.Id == _serviceState.Id);
				if (myIndex >= 0) { //found and not announcer => go back
					await info("found and not announcer => go back");
					destinationService = model.Services[myIndex - 1];
				} else { //not found, forward this notification
					if (((model.Sender == _nextNeighbour.Id) && (model.DeadServiceId == _previousNeighbour.Id))
						|| ((model.Sender == _previousNeighbour.Id) && (model.DeadServiceId == _nextNeighbour.Id))
						|| ((_previousNeighbour.Id == _nextNeighbour.Id) && (_previousNeighbour.Id != model.DeadServiceId))
						|| ((model.AnnouncerId == _nextNeighbour.Id) && (model.Sender != _nextNeighbour.Id))) {
						//if next planned destination is the actual dead service and previous neighbour is sender
						//OR current service has only one neigbour (the same ID on prev and next neighbour), return the message
						//OR the next in line is annoucer (should have occurred after the current service has already processed the dead service and rejoined with the announcer)
						await info("if next planned destination is the actual dead service and previous neighbour is sender, return the message to the announcer");
						destinationService = model.Services.First(x => x.Id == model.AnnouncerId);
					} else {
						await info("Just send it to the next neighbour");
						await info($"Sender = {model.Services.First(x => x.Id == model.Sender).NodeId}");
						await info($"Next neighbour = {_nextNeighbour.NodeId}");
						await info($"Previous neighbour = {_previousNeighbour.NodeId}");
						//otherwise send it to the next neighbour (who is not the sender of current message)
						destinationService = model.Sender == _previousNeighbour.Id ? _nextNeighbour : _previousNeighbour;
					}
					model.Services.Add(getSenderState());
				}
				if ((destinationService.Id == model.DeadServiceId) || (destinationService.Id == Guid.Empty))
					//if somehow the destination service ended up being the dead service, send the message back to announcer
					destinationService = model.Services.First(x => x.Id == model.AnnouncerId);
				model.Sender = _serviceState.Id;

				await _sender.Send(model, getReachableEndpoint(destinationService.Endpoints));
				await info($"Replied to service [{destinationService.NodeId}] on a dead service notification.");
			});
		}

		void updateNeighbours(DeadServiceModel<T> model) {
			if ((_previousNeighbour.Id != Guid.Empty) && (_nextNeighbour.Id != Guid.Empty))
				return;
			foreach (var service in model.Services.Where(x => x.Id != _serviceState.Id)) {
				addHost(service);
			}
			setNeighbours();
			setWatchOnNeighbours();
		}

		void handleSuspiciousService(SuspiciousServiceModel<T> model) {
			Task.Factory.StartNew(async () => {
				if (model.AnnouncerId == _serviceState.Id) {
					if ((model.Downtime > SERVICE_TIMEOUT) && _suspiciousServices.ContainsKey(model.SuspiciousServiceId)) {
						await info("Suspicious service notification made the full circle and suspicious service is dead indeed.");
						await notifyDeadService(model);
						deleteDeadNeighbour(model.SuspiciousServiceId);
					} else {
						await info($"Suspicious service detected by other services with {model.Downtime} downtime, removing from suspicious services list.");
						if (_suspiciousServices.ContainsKey(model.SuspiciousServiceId))
							_suspiciousServices.Remove(model.SuspiciousServiceId);
					}
					return;
				}
				BaseServiceState destinationService;
				var suspiciousServiceKnown = false;
				var serviceInactive = false;

				await info($"Suspicious service notification received: {model.SuspiciousServiceId} downtime: {model.Downtime} sender: {model.Sender}");
				if (_watchedServices.ContainsKey(model.SuspiciousServiceId)) {
					var downtime = (DateTime.Now - _watchedServices[model.SuspiciousServiceId].LastHeartbeat).TotalMilliseconds;
					model.Downtime = downtime;
					suspiciousServiceKnown = true;
					serviceInactive = downtime > SERVICE_TIMEOUT;
				}
				if (model.SuspiciousServiceId == _serviceState.Id)
					model.Downtime = 0;
				if ((suspiciousServiceKnown && serviceInactive) || (model.SuspiciousServiceId == _serviceState.Id))
					destinationService = model.Services.First();
				else {
					var myIndex = model.Services.FindIndex(x => x.Id == _serviceState.Id);
					if (myIndex >= 0) { //found and not announcer => go back
						await info("found and not announcer => go back");
						destinationService = model.Services[myIndex - 1];
					} else { //not found, forward this notification
						if (((_previousNeighbour.Id == _nextNeighbour.Id) && (_previousNeighbour.Id == model.Sender))
							|| ((model.Sender == model.AnnouncerId) && (model.SuspiciousServiceId == _serviceState.Id))) {
							//if current service has only one neigbour (the same ID on prev and next neighbour)
							//OR current service is the suspicious service
							//return message
							await info("I have only one neighbour OR I'm flagged as the suspicious service");
							destinationService = model.Services.Last();
						} else {
							await info("otherwise send it to the next neighbour (who is not the sender of current message)");
							//otherwise send it to the next neighbour (who is not the sender of current message)
							destinationService = model.Sender == _previousNeighbour.Id ? _nextNeighbour : _previousNeighbour;
						}
						model.Services.Add(getSenderState());
					}
				}

				model.Sender = _serviceState.Id;
				await _sender.Send(model, getReachableEndpoint(destinationService.Endpoints));
				await info($"Replied to neighbour [{destinationService.NodeId}] for suspicious service [{model.HeartbeatModel.NodeId}] with downtime {model.Downtime}");
			});
		}

		async Task notifyDeadService(SuspiciousServiceModel<T> model) {
			var deadServiceModel = new DeadServiceModel<T> {
				Sender = _serviceState.Id,
				DeadServiceId = model.SuspiciousServiceId,
				Services = new List<BaseServiceState> { getSenderState() },
				AnnouncerId = _serviceState.Id,
				IsLeader = model.IsLeader,
				LeaderState = model.LeaderState
			};

			var destination = model.SuspiciousServiceId == _previousNeighbour.Id ? _nextNeighbour : _previousNeighbour;
			await _sender.Send(deadServiceModel, getReachableEndpoint(_previousNeighbour.Endpoints));

			await info($"Neighbour notified for dead service: {destination.NodeId}");
		}

		void deleteDeadNeighbour(Guid deadServiceId) {
			if (_previousNeighbour.Id == deadServiceId)
				_previousNeighbour = new ServiceState<T>();
			if (_nextNeighbour.Id == deadServiceId)
				_nextNeighbour = new ServiceState<T>();
			_hbService.Targets = getHeartbeatTargets();
		}

		void handleNeighbourUpdate(NeighbourUpdateModel model) {
			//ignore if model is null
			//ignore if this service already processed neighbour update for current model
			if ((model == null) || model.ProcessedServices.Any(x => x.Id == _serviceState.Id))
				return;
			Task.Factory.StartNew(async () => {
				addHost(_previousNeighbour);
				addHost(_nextNeighbour);
				addHost(model.SenderState);
				foreach (var processedService in model.ProcessedServices) {
					addHost(processedService);
				}
				_allHosts = _allHosts.Where(x => (x.Key != _serviceState.Id) && (x.Key != Guid.Empty)).ToDictionary(p => p.Key, p => p.Value);
				setNeighbours();
				setWatchOnNeighbours();
				await printNeighbours();
				model.ProcessedServices.Add(getSenderState());
				await info("Neighbour update handled by the following services: ");
				foreach (var processedService in model.ProcessedServices) {
					int value;
					_newServices.TryRemove(processedService.Id, out value);
					await info(processedService.NodeId);
				}
				await notifyNeighbourUpdate(_previousNeighbour, model);
			});
		}

		void setWatchOnNeighbours() {
			_watchedServices.Clear();
			_watchedServices.TryAdd(
				_previousNeighbour.Id,
				new HeartbeatModel<T> {
					Id = _previousNeighbour.Id,
					NodeId = _previousNeighbour.NodeId,
					IsLeader = _previousNeighbour.Id == _serviceState.LeaderState.Id,
					LastHeartbeat = DateTime.MaxValue
				});
			if (_previousNeighbour.Id != _nextNeighbour.Id)
				_watchedServices.TryAdd(
					_nextNeighbour.Id,
					new HeartbeatModel<T> {
						Id = _nextNeighbour.Id,
						NodeId = _nextNeighbour.NodeId,
						IsLeader = _nextNeighbour.Id == _serviceState.LeaderState.Id,
						LastHeartbeat = DateTime.MaxValue
					});
		}

		async Task printNeighbours() {
			await info($"Left neighbour: {_previousNeighbour.NodeId}");
			await info($"Right neighbour: {_nextNeighbour.NodeId}");
		}

		void handleReceivedHeartbeat(HeartbeatModel<T> model) {
			if (model.Id == _serviceState.Id)
				return;

			Task.Factory.StartNew(() => {
				HeartbeatModel<T> savedModel;
				if (!_watchedServices.TryGetValue(model.Id, out savedModel))
					return;
				//await info($"Received heartbeat from {model.NodeId}");
				savedModel.LastHeartbeat = DateTime.Now;
				if (model.IsLeader) {
					_serviceState.LeaderState = model.LeaderState;
					_hbService.SenderState = _serviceState;
					savedModel.LeaderState = model.LeaderState;
					savedModel.IsLeader = true;
				} else
					_watchedServices[model.Id] = savedModel;
			});
		}

		static NodeNotificationModel bytesToNotificationModel(byte[] bytes) {
			var strData = Encoding.UTF8.GetString(bytes);
			return JsonConvert.DeserializeObject<NodeNotificationModel>(strData);
		}

		void setNeighbours() {
			if (!_allHosts.Any())
				return;
			var behind = _allHosts.Where(x => x.Value.NodeId < _serviceState.NodeId).ToArray();
			BaseServiceState previous;
			if (!behind.Any()) {
				var maxOrderNumber = _allHosts.Max(x => x.Value.NodeId);
				previous = _allHosts.FirstOrDefault(x => x.Value.NodeId == maxOrderNumber).Value;
			} else {
				var maxOrderNumber = behind.Max(x => x.Value.NodeId);
				previous = behind.FirstOrDefault(x => x.Value.NodeId == maxOrderNumber).Value;
			}
			_previousNeighbour = previous;

			var ahead = _allHosts.Where(x => x.Value.NodeId > _serviceState.NodeId).ToArray();
			BaseServiceState next;
			if (!ahead.Any()) {
				var minOrderNumber = _allHosts.Min(x => x.Value.NodeId);
				next = _allHosts.FirstOrDefault(x => x.Value.NodeId == minOrderNumber).Value;
			} else {
				var minOrderNumber = ahead.Min(x => x.Value.NodeId);
				next = ahead.FirstOrDefault(x => x.Value.NodeId == minOrderNumber).Value;
			}
			_nextNeighbour = next;
			_hbService.Targets = getHeartbeatTargets();

			_allHosts.Clear();
		}

		void addHost(BaseServiceState model) {
			if (!_allHosts.ContainsKey(model.Id))
				_allHosts.Add(model.Id, model);
		}

		BaseServiceState getSenderState() {
			return new BaseServiceState {
				NodeId = _serviceState.NodeId,
				Id = _serviceState.Id,
				Endpoints = _serviceState.Endpoints
			};
		}

		async Task setInitializedValues() {
			_initialized = true;

			if (_serviceState.IsLeader || (_serviceState.LeaderLeftNeighbour.Id != Guid.Empty) || (_serviceState.LeaderRightNeighbour.Id != Guid.Empty)) {
				addHost(_serviceState.LeaderLeftNeighbour);
				addHost(_serviceState.LeaderRightNeighbour);
			}
			addHost(_serviceState.LeaderServiceState);

			setNeighbours();
			if (!_serviceState.IsLeader)
				setWatchOnNeighbours();

			var destinationNode = _serviceState.LeaderLeftNeighbour.Id == Guid.Empty ? _serviceState.LeaderServiceState : _serviceState.LeaderLeftNeighbour;
			if (_serviceState.Id != destinationNode.Id) {
				var updateModel = new NeighbourUpdateModel {
					ProcessedServices = new List<BaseServiceState> { getSenderState() }
				};
				await notifyNeighbourUpdate(destinationNode, updateModel);
			}
		}

		async Task notifyNeighbourRegroup(DeadServiceModel<T> deadServiceModel) {
			var model = new NeighbourRegroupModel<T> {
				Sender = _serviceState.Id,
				Announcer = _serviceState.Id,
				AllServices = deadServiceModel.Services,
				LeaderDown = deadServiceModel.IsLeader,
				LeaderState = deadServiceModel.LeaderState
			};
			//TODO: no idea why this goes clock-wise (all the other notifications go counter-clock-wise)
			await _sender.Send(model, getReachableEndpoint(_nextNeighbour.Endpoints));
		}

		async Task notifyNeighbourUpdate(BaseServiceState destinationNode, NeighbourUpdateModel model) {
			var destinationEndpoint = getReachableEndpoint(destinationNode.Endpoints);
			if ((destinationEndpoint == null) || (destinationNode.NodeId == 0) || (destinationNode.Id == Guid.Empty))
				return;

			model.SenderState = getSenderState();
			await _sender.Send(model, destinationEndpoint);
		}
	}
}