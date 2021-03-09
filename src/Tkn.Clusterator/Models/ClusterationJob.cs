using System;
using System.Threading.Tasks;

namespace Tkn.Clusterator.Models {
	public class ClusterationJob<T> where T : LeaderState {
		public bool RequiresLeader { get; set; } = true;
		public Func<T, Task> ExecutionTask { get; set; }
	}
}