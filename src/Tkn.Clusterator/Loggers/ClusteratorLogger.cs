using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tkn.Clusterator.Interfaces;

namespace Tkn.Clusterator.Loggers {
	public class ClusteratorLogger : ILogger {
		readonly IEnumerable<ILogWriter> _writers;
		readonly ILoggingConfig _config;

		public ClusteratorLogger(IEnumerable<ILogWriter> writers, ILoggingConfig config) {
			_writers = writers;
			_config = config;
		}

		public async Task Debug(string message) {
			await logForLevel($"{DateTime.Now:G} {message}", LogLevel.Debug);
		}

		public async Task Info(string message) {
			await logForLevel($"{DateTime.Now:G} {message}", LogLevel.Info);
		}

		public async Task Error(string message) {
			await logForLevel($"{DateTime.Now:G} {message}", LogLevel.Error);
		}

		public async Task Fatal(string message) {
			await logForLevel($"{DateTime.Now:G} {message}", LogLevel.Fatal);
		}

		async Task logForLevel(string message, LogLevel level) {
			if (_config.Level > level)
				return;
			var tasks = _writers.Select(logWriter => logWriter.Write(message));
			await Task.WhenAll(tasks);
		}
	}
}