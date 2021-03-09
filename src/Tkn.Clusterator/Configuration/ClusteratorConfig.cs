using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Tkn.Clusterator.Loggers;

namespace Tkn.Clusterator.Configuration {
	public sealed class ClusteratorConfig : IBroadcastConfig, IDiscoveryConfig, IHeartbeatConfig, ILoggingConfig {
		const string BROADCAST_SECTION = "Tkn.Clusterator:BroadcastSettings";
		const string DISCOVERY_SECTION = "Tkn.Clusterator:DiscoverySettings";
		const string HEARTBEAT_SECTION = "Tkn.Clusterator:HeartbeatSettings";
		const string LOGGING_SECTION = "Tkn.Clusterator:LoggingSettings";

		readonly IConfigurationRoot _config;

		public ClusteratorConfig(IConfigurationRoot config) {
			_config = config;
		}

		public int BroadcastPort => Convert.ToInt32(_config[$"{BROADCAST_SECTION}:Port"]);
		public int ListeningTimeout => Convert.ToInt32(_config[$"{BROADCAST_SECTION}:ListenTimeout"]);
		public IEnumerable<string> ExcludedAdapters => _config[$"{BROADCAST_SECTION}:ExcludedAdapters"].Split(';');

		public int InitializeTimeout => Convert.ToInt32(_config[$"{DISCOVERY_SECTION}:InitTimeout"]);
		public int InitialNodeId => Convert.ToInt32(_config[$"{DISCOVERY_SECTION}:InitialNodeId"]);
		public string ClusterId => _config[$"{DISCOVERY_SECTION}:ClusterId"] ?? string.Empty;

		public int HeartbeatInterval => Convert.ToInt32(_config[$"{HEARTBEAT_SECTION}:IntervalInMs"]);
		public LogLevel Level {
			get {
				LogLevel value;
				if (!Enum.TryParse(_config[$"{LOGGING_SECTION}:Level"] ?? string.Empty, true, out value))
					value = LogLevel.None;
				return value;
			}
		}
	}
}