namespace Tkn.Clusterator.Models {
	internal class NodeNotificationModel {
		public NodeNotificationType NotificationType { get; set; }
		public string Data { get; set; }
		public string ClusterId { get; set; }
	}
}