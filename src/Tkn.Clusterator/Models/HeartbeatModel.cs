using System;

namespace Tkn.Clusterator.Models {
	internal class HeartbeatModel<T> : BaseNotificationByteConvertible where T : LeaderState {
		public Guid Id { get; set; }
		public int NodeId { get; set; }
		public bool IsLeader { get; set; }
		public DateTime LastHeartbeat { get; set; }
		public T LeaderState { get; set; }
		public override NodeNotificationType NotificationType { get; set; } = NodeNotificationType.Heartbeat;
	}
}