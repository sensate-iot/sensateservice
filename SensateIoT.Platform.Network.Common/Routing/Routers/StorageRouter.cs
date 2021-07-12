/*
 * Storage service router.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Abstract;
using SensateIoT.Platform.Network.Data.DTO;
using SensateIoT.Platform.Network.Common.Collections.Abstract;
using SensateIoT.Platform.Network.Common.Routing.Abstract;

namespace SensateIoT.Platform.Network.Common.Routing.Routers
{
	public class StorageRouter : IRouter
	{
		private readonly IRemoteStorageQueue m_storageQueue;

		public string Name => "Storage Router";

		public StorageRouter(IRemoteStorageQueue queue)
		{
			this.m_storageQueue = queue;
		}

		public bool Route(Sensor sensor, IPlatformMessage message, NetworkEvent networkEvent)
		{
			if(!sensor.StorageEnabled || message.Type == MessageType.ControlMessage) {
				return true;
			}

			this.m_storageQueue.Enqueue(message);
			networkEvent.Actions.Add(NetworkEventType.MessageStorage);

			return true;
		}
	}
}
