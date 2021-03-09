using System;

namespace Tkn.Clusterator.Models {
	internal class LeaderElectionModel<T> : BaseNotificationByteConvertible where T : LeaderState {
		public int CandidateNodeId { get; set; }
		public bool Completed { get; set; }
		public Guid CandidateId { get; set; }
		public T PreviousLeaderState { get; set; }

		public override NodeNotificationType NotificationType { get; set; } = NodeNotificationType.LeaderElection;
	}
}