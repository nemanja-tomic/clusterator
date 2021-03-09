using Tkn.Clusterator.Common;
using Tkn.Clusterator.Interfaces;

namespace Tkn.Clusterator.Models {
	internal abstract class BaseNotificationByteConvertible : BaseByteConvertible, INotificationModel {
		public abstract NodeNotificationType NotificationType { get; set; }

		public override byte[] ToBytes() {
			return this.ToNotificationModelBytes(NotificationType, ClusterId);
		}
	}
}