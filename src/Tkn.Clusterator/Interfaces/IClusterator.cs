using System;
using System.Threading.Tasks;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Interfaces {
	public interface IClusterator<T> : IDisposable where T : LeaderState {
		Task Clusterize(ClusterationJob<T> job);
		Task UpdateLeaderState(T newState);
	}
}