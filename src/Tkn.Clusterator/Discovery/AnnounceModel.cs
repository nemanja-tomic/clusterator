using System;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Discovery {
	internal class AnnounceModel : BaseByteConvertible {
		public Guid AnnouncerId { get; set; }
		public ServiceEndpoint Endpoint { get; set; }
	}
}