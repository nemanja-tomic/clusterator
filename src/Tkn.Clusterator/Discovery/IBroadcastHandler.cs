using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Discovery {
	internal interface IBroadcastHandler : IDisposable {
		void Subscribe(Action<AnnounceModel> handler);
		Task<IEnumerable<ServiceEndpoint>> Broadcast(AnnounceModel announceModel);
	}
}