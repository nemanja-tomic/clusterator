using System;
using System.Collections.Generic;

namespace Tkn.Clusterator.Models {
	internal class NeighbourRegroupModel<T> : BaseNotificationByteConvertible where T : LeaderState {
		public List<BaseServiceState> AllServices { get; set; } = new List<BaseServiceState>();
		public Guid Announcer { get; set; }
		public Guid Sender { get; set; }
		public bool LeaderDown { get; set; }
		public T LeaderState { get; set; }
		public override NodeNotificationType NotificationType { get; set; } = NodeNotificationType.NeighbourRegroup;
	}
}