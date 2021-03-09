using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tkn.Clusterator.Configuration;
using Tkn.Clusterator.Discovery;
using Tkn.Clusterator.Heartbeat;
using Tkn.Clusterator.Interfaces;
using Tkn.Clusterator.Loggers;

namespace Tkn.Clusterator.Bootstrap {
	public static class ServiceCollectionExtensions {
		public static void AddClusterator<T1>(this IServiceCollection services, IConfigurationRoot config)
			where T1 : class, IDiscoveryConfig, IBroadcastConfig, IHeartbeatConfig, ILoggingConfig, new() {
			//TODO: OS (windows/linux) awareness
			services.AddSingleton(typeof(IConfigurationRoot), config);
			services.AddSingleton<IBroadcastConfig, T1>();
			services.AddSingleton<IDiscoveryConfig, T1>();
			services.AddSingleton<IHeartbeatConfig, T1>();
			services.AddSingleton<ILoggingConfig, T1>();
			services.AddSingleton<T1>();

			mapServices(services);
		}

		public static void AddClusterator(this IServiceCollection services, IConfigurationRoot config) {
			services.AddSingleton(typeof(IConfigurationRoot), config);
			services.AddSingleton<ClusteratorConfig>();
			services.AddSingleton<IBroadcastConfig, ClusteratorConfig>();
			services.AddSingleton<IDiscoveryConfig, ClusteratorConfig>();
			services.AddSingleton<IHeartbeatConfig, ClusteratorConfig>();
			services.AddSingleton<ILoggingConfig, ClusteratorConfig>();
			mapServices(services);
		}

		static void mapServices(IServiceCollection services) {
			services.AddTransient<ILogger, ClusteratorLogger>();
			services.AddTransient<ILogWriter, ConsoleLogWriter>();
			services.AddTransient<IBroadcastHandler, UdpBroadcastHandler>();
			services.AddTransient<IMessageSender, UdpMessageSender>();
			services.AddTransient(typeof(IDiscoveryService<>), typeof(DiscoveryService<>));
			services.AddTransient(typeof(IHeartbeatService<>), typeof(HeartbeatService<>));

			//TODO: should be moved to a separate DLL, so any number of new network topologies can be implemented and separately used.
			services.AddTransient(typeof(IClusterator<>), typeof(RingeRajaClusterator<>));
		}
	}
}