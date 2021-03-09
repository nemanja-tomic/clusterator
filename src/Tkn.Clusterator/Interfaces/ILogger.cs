using System.Threading.Tasks;

namespace Tkn.Clusterator.Interfaces {
	public interface ILogger {
		Task Debug(string message);
		Task Info(string message);
		Task Error(string message);
		Task Fatal(string message);
	}
}