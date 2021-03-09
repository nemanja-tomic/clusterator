using System;

namespace Tkn.Clusterator.Discovery.Events {
	internal class HostAnnouncedEventArgs : EventArgs {
		public AnnounceModel Model { get; set; }

		public HostAnnouncedEventArgs(AnnounceModel model) {
			Model = model;
		}
	}
}