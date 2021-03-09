using System;
using System.Text;
using Newtonsoft.Json;
using Tkn.Clusterator.Interfaces;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Common {
	public static class ExtensionMethods {
		public static byte[] ToBytes(this object model) {
			var json = JsonConvert.SerializeObject(model);
			return Encoding.UTF8.GetBytes(json);
		}

		public static T ToModel<T>(this byte[] bytes) {
			var json = Encoding.UTF8.GetString(bytes);
			return JsonConvert.DeserializeObject<T>(json);
		}

		public static bool TryToModel<T>(this byte[] bytes, out T model) where T : class {
			model = null;
			try {
				var json = Encoding.UTF8.GetString(bytes);
				model = JsonConvert.DeserializeObject<T>(json);
				return true;
			} catch (Exception) {
				return false;
			}
		}

		internal static byte[] ToNotificationModelBytes<T>(this T model, NodeNotificationType notificationType, string clusterId) where T : class, INotificationModel {
			var notificationModel = new NodeNotificationModel {
				NotificationType = notificationType,
				Data = JsonConvert.SerializeObject(model),
				ClusterId = clusterId
			};
			var json = JsonConvert.SerializeObject(notificationModel);
			var bytes = Encoding.UTF8.GetBytes(json);
			return bytes;
		}
	}
}