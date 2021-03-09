using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Tkn.Clusterator.Configuration;

namespace Tkn.Clusterator.Integration {
	public class Configuration : IDiscoveryConfig, IBroadcastConfig, IHeartbeatConfig {
		const string BROADCAST_SECTION = "Tkn.Clusterator:BroadcastSettings";
		const string DISCOVERY_SECTION = "Tkn.Clusterator:DiscoverySettings";
		const string HEARTBEAT_SECTION = "Tkn.Clusterator:HeartbeatSettings";
		readonly IConfigurationRoot _config;

		public Configuration(IConfigurationRoot config) {
			_config = config;
		}

		public int BroadcastPort => Convert.ToInt32(_config[$"{BROADCAST_SECTION}:Port"]);
		public int ListeningTimeout => Convert.ToInt32(_config[$"{BROADCAST_SECTION}:ListenTimeout"]);
		public IEnumerable<string> ExcludedAdapters => _config[$"{BROADCAST_SECTION}:ExcludedAdapters"].Split(';');

		public int InitializeTimeout => Convert.ToInt32(_config[$"{DISCOVERY_SECTION}:InitTimeout"]);
		public int InitialNodeId => Convert.ToInt32(_config[$"{DISCOVERY_SECTION}:InitialNodeId"]);
		public string ClusterId => _config[$"{DISCOVERY_SECTION}:ClusterId"] ?? string.Empty;
		public int HeartbeatInterval => Convert.ToInt32(_config[$"{HEARTBEAT_SECTION}:IntervalInMs"]);
	}
}