namespace Tkn.Clusterator.Models {
	internal enum NodeNotificationType {
		NeighbourUpdate,
		NeighbourRegroup,
		DeadService,
		SuspiciousService,
		LeaderElection,
		Heartbeat
	}
}