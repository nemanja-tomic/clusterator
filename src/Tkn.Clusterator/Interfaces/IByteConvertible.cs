namespace Tkn.Clusterator.Interfaces {
	public interface IByteConvertible {
		byte[] ToBytes();
		string ClusterId { get; set; }
	}
}