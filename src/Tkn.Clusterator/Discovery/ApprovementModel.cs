using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Discovery {
	internal class ApprovementModel<T> : BaseByteConvertible where T : LeaderState, new() {
		public T LeaderState { get; set; }
		public int AssignedNodeId { get; set; }
		public BaseServiceState LeaderPreviousNeighbour { get; set; }
		public BaseServiceState LeaderNextNeighbour { get; set; }
		public BaseServiceState LeaderServiceState { get; set; }
	}
}