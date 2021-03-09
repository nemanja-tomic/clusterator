namespace Tkn.Clusterator.Configuration {
	public interface IDiscoveryConfig {
		int InitializeTimeout { get; }
		int InitialNodeId { get; }
		string ClusterId { get; }
	}
}