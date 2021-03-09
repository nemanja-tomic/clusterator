using System;
using System.Collections.Generic;

namespace Tkn.Clusterator.Models {
	public class BaseServiceState {
		public Guid Id { get; set; }
		public int NodeId { get; set; }
		public IEnumerable<ServiceEndpoint> Endpoints { get; set; } = new ServiceEndpoint[] { };
	}
}