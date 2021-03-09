namespace Tkn.Clusterator.Models {
	public class ServiceState<T> : BaseServiceState where T : LeaderState {
		readonly object _lockObject = new object();
		T _leaderState;

		public bool IsLeader { get; set; }
		public T LeaderState {
			get { return _leaderState; }
			set {
				lock (_lockObject) {
					_leaderState = value;
				}
			}
		}
		public BaseServiceState LeaderServiceState { get; set; }
		public BaseServiceState LeaderLeftNeighbour { get; set; }
		public BaseServiceState LeaderRightNeighbour { get; set; }
	}
}