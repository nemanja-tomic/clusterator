using System;
using System.Threading.Tasks;

namespace Tkn.Clusterator.Loggers {
	public class ConsoleLogWriter : ILogWriter {
		public async Task Write(string message) {
			await Task.Run(() => Console.Write(message));
		}
	}
}