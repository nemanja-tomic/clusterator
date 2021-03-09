using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tkn.Clusterator.Configuration;
using Tkn.Clusterator.Interfaces;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Heartbeat {
	internal class HeartbeatService<T> : IHeartbeatService<T> where T : LeaderState {
		readonly IMessageSender _sender;
		readonly IHeartbeatConfig _config;
		readonly ILogger _logger;
		readonly Timer _heartbeatTimer;
		ServiceState<T> _senderState;
		readonly object _lockObject = new object();

		public IEnumerable<ServiceEndpoint> Targets { get; set; }

		public ServiceState<T> SenderState {
			get { return _senderState; }
			set {
				lock (_lockObject) {
					_senderState = value;
				}
			}
		}

		public HeartbeatService(IHeartbeatConfig config, ILogger logger, IMessageSender sender) {
			_config = config;
			_logger = logger;
			_sender = sender;
			_heartbeatTimer = new Timer(sendHeartbeat, null, Timeout.Infinite, Timeout.Infinite);
		}

		public async Task Start() {
			await Task.Run(() => _heartbeatTimer.Change(0, _config.HeartbeatInterval));
		}

		public async Task Stop() {
			await Task.Run(() => _heartbeatTimer.Change(Timeout.Infinite, Timeout.Infinite));
		}

		async void sendHeartbeat(object args) {
			var tasks = new List<Task>();
			foreach (var target in Targets.Distinct().Where(x => !string.IsNullOrEmpty(x?.Host) && (x.Port > 0))) {
				var model = new HeartbeatModel<T> {
					Id = SenderState.Id,
					NodeId = SenderState.NodeId,
					IsLeader = SenderState.IsLeader,
					LeaderState = SenderState.IsLeader ? SenderState.LeaderState : null
				};
				tasks.Add(_sender.Send(model, target));
				await log($"Heartbeat sent to: {target.Host} through {target.Port}");
			}
			await Task.WhenAll(tasks);
		}

		async Task log(string message) {
			return;
			await _logger.Info($"Heartbeat service: {message}");
		}
	}
}