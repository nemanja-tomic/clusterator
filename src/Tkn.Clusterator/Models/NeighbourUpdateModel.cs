using System.Collections.Generic;

namespace Tkn.Clusterator.Models {
	internal class NeighbourUpdateModel : BaseNotificationByteConvertible{
		public BaseServiceState SenderState { get; set; }
		public List<BaseServiceState> ProcessedServices { get; set; } = new List<BaseServiceState>();
		public override NodeNotificationType NotificationType { get; set; } = NodeNotificationType.NeighbourUpdate;
	}
}