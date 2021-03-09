using System.Collections.Generic;
using System.Threading.Tasks;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Heartbeat {
	public interface IHeartbeatService<T> where T : LeaderState {
		Task Start();
		Task Stop();
		IEnumerable<ServiceEndpoint> Targets { get; set; }
		ServiceState<T> SenderState { get; set; }
	}
}