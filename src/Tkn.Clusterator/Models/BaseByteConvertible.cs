using Tkn.Clusterator.Common;
using Tkn.Clusterator.Interfaces;

namespace Tkn.Clusterator.Models {
	internal abstract class BaseByteConvertible : IByteConvertible {
		public virtual byte[] ToBytes() {
			return ExtensionMethods.ToBytes(this);
		}
		public virtual string ClusterId { get; set; }
	}
}