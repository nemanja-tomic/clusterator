using System;
using System.Collections.Generic;

namespace Tkn.Clusterator.Models {
	internal class SuspiciousServiceModel<T> : BaseNotificationByteConvertible where T : LeaderState {
		public double Downtime { get; set; }
		public Guid AnnouncerId { get; set; }
		public Guid SuspiciousServiceId { get; set; }
		public Guid Sender { get; set; }
		public List<BaseServiceState> Services { get; set; } = new List<BaseServiceState>();
		public HeartbeatModel<T> HeartbeatModel { get; set; }
		public bool IsLeader { get; set; }
		public T LeaderState { get; set; }
		public override NodeNotificationType NotificationType { get; set; } = NodeNotificationType.SuspiciousService;
	}
}