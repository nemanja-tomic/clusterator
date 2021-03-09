using System.Threading.Tasks;
using Tkn.Clusterator.Discovery.Events;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Discovery {
	internal interface IDiscoveryService<T> where T : LeaderState, new() {
		Task<ServiceState<T>> Initialize();
		event HostAnnouncedHandler HostAnnounced;
		Task Approve(ApprovementModel<T> model, ServiceEndpoint endpoint);
	}
}