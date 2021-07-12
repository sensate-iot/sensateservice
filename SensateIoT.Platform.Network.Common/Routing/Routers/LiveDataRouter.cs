/*
 * Live data router.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System.Linq;

using Microsoft.Extensions.Logging;

using SensateIoT.Platform.Network.Common.Collections.Abstract;
using SensateIoT.Platform.Network.Common.Exceptions;
using SensateIoT.Platform.Network.Common.Routing.Abstract;
using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Abstract;
using SensateIoT.Platform.Network.Data.DTO;

namespace SensateIoT.Platform.Network.Common.Routing.Routers
{
	public class LiveDataRouter : IRouter
	{
		private readonly ILogger<LiveDataRouter> m_logger;
		private readonly IInternalRemoteQueue m_internalQueue;

		public string Name => "Live Data Router";

		public LiveDataRouter(ILogger<LiveDataRouter> logger, IInternalRemoteQueue queue)
		{
			this.m_internalQueue = queue;
			this.m_logger = logger;
		}

		public bool Route(Sensor sensor, IPlatformMessage message, NetworkEvent networkEvent)
		{
			if(sensor.LiveDataRouting == null || sensor.LiveDataRouting?.Count <= 0) {
				return true;
			}

			switch(message.Type) {
			case MessageType.Measurement:
			case MessageType.Message:
			case MessageType.ControlMessage:
				return this.ProcessMessage(sensor, message, networkEvent);

			default:
				this.m_logger.LogError("Received invalid message type. Unable to route to live data service. " +
									   "The received type is: {type}", message.Type);
				throw new RouterException(nameof(LiveDataRouter), $"unable to route message of type {message.Type:G}");
			}
		}

		private bool ProcessMessage(Sensor sensor, IPlatformMessage message, NetworkEvent evt)
		{
			evt.Actions.Add(NetworkEventType.MessageLiveData);
			var routes = sensor.LiveDataRouting.ToList(); // Take a snapshot

			foreach(var info in routes) {
				this.m_logger.LogDebug("Routing message to live data client: {clientId}.", info.Target);
				this.EnqueueTo(message, info);
			}

			return true;
		}

		private void EnqueueTo(IPlatformMessage message, RoutingTarget target)
		{
			switch(message.Type) {
			case MessageType.ControlMessage:
				this.m_internalQueue.EnqueueControlMessageToTarget(message, target);
				break;
			case MessageType.Message:
				this.m_internalQueue.EnqueueMessageToTarget(message, target);
				break;

			case MessageType.Measurement:
				this.m_internalQueue.EnqueueMeasurementToTarget(message, target);
				break;
			}
		}
	}
}
