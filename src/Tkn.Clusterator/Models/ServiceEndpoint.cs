using System;
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace Tkn.Clusterator.Models {
	public class ServiceEndpoint : IEquatable<ServiceEndpoint> {
		public ServiceEndpoint() {
		}

		public ServiceEndpoint(string host, int port) {
			Host = host;
			Port = port;
		}

		public string Host { get; set; }
		public string MaskPrefix { get; set; }
		public int Port { get; set; }

		public bool Equals(ServiceEndpoint other) {
			if (other == null)
				return false;

			return GetHashCode() == other.GetHashCode();
		}

		public override bool Equals(object obj) {
			return Equals(obj as ServiceEndpoint);
		}

		public override int GetHashCode() {
			return (string.IsNullOrEmpty(Host) ? 0 : Host.GetHashCode()) ^ Port.GetHashCode();
		}
	}
}