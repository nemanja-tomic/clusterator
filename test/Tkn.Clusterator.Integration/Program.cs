using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tkn.Clusterator.Bootstrap;
using Tkn.Clusterator.Interfaces;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Integration {
	public class Program {
		static IServiceProvider _serviceProvider;
		static IClusterator<Penis> _clusterator;

		public static void Main(string[] args) {
			Console.WriteLine("Starting...");
			var osDescription = RuntimeInformation.OSDescription;
			Console.WriteLine($"OS Version: {osDescription}");

			try {
				var builder = new ConfigurationBuilder()
					.AddJsonFile("appsettings.json")
					.AddEnvironmentVariables();
				var configurationRoot = builder.Build();

				IServiceCollection serviceCollection = new ServiceCollection();
				serviceCollection.AddClusterator(configurationRoot);
				_serviceProvider = serviceCollection.BuildServiceProvider();

				_clusterator = _serviceProvider.GetService<IClusterator<Penis>>();

				Task.WaitAll(_clusterator.Clusterize(new ClusterationJob<Penis> {
					RequiresLeader = true,
					ExecutionTask = job
				}));
			} catch (Exception ex) {
				Console.WriteLine(ex.Message);
			} finally {
				_clusterator.Dispose();
			}
			Console.WriteLine("Application Finished");
		}

		static async Task job(Penis data) {
			while (data.SomeData <= 10 || true) {
				data.SomeData++;
				await _clusterator.UpdateLeaderState(data);
				if (data.SomeData % 5 == 0)
					Console.WriteLine($"progress: {data.SomeData}");
				//Console.WriteLine($"I'm the fucking leader now and this is the next node id => {data.NextNodeId}!");
				await Task.Delay(1000);
			}
		}

		class Penis : LeaderState {
			public int SomeData { get; set; }
			public string SomeString { get; set; }
		}
	}
}
