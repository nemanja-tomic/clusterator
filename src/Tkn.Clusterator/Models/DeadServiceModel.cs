using System;
using System.Collections.Generic;

namespace Tkn.Clusterator.Models {
	internal class DeadServiceModel<T> : BaseNotificationByteConvertible where T : LeaderState {
		public Guid Sender { get; set; }
		public Guid DeadServiceId { get; set; }
		public List<BaseServiceState> Services { get; set; } = new List<BaseServiceState>();
		public Guid AnnouncerId { get; set; }
		public bool IsLeader { get; set; }
		public T LeaderState { get; set; }
		public override NodeNotificationType NotificationType { get; set; } = NodeNotificationType.DeadService;
	}
}