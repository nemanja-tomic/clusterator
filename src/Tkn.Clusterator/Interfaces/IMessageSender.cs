using System.Threading.Tasks;
using Tkn.Clusterator.Discovery;
using Tkn.Clusterator.Models;

namespace Tkn.Clusterator.Interfaces {
	internal interface IMessageSender {
		Task Send(IByteConvertible model, ServiceEndpoint target);
		Task Broadcast(IByteConvertible announceModel, string localIp, int broadcastPort);
	}
}