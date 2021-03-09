using System.Collections.Generic;

namespace Tkn.Clusterator.Configuration {
	public interface IBroadcastConfig {
		int BroadcastPort { get; }
		int ListeningTimeout { get; }
		IEnumerable<string> ExcludedAdapters { get; }
	}
}