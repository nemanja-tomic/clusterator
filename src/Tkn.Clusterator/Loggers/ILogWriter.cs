using System.Threading.Tasks;

namespace Tkn.Clusterator.Loggers {
	public interface ILogWriter {
		Task Write(string message);
	}
}