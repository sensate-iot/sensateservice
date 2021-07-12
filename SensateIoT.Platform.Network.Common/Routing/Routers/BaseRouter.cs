/*
 * Base message router. Should be executed first.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using Microsoft.Extensions.Logging;
using SensateIoT.Platform.Network.Common.Routing.Abstract;
using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Abstract;
using SensateIoT.Platform.Network.Data.DTO;

namespace SensateIoT.Platform.Network.Common.Routing.Routers
{
	public class BaseRouter : IRouter
	{
		private readonly ILogger<BaseRouter> m_logger;

		public BaseRouter(ILogger<BaseRouter> logger)
		{
			this.m_logger = logger;
		}

		public string Name => "Base Router";

		public bool Route(Sensor sensor, IPlatformMessage message, NetworkEvent networkEvent)
		{
			var result = true;

			switch(message.Type) {
			case MessageType.Measurement:
				networkEvent.MessageType = NetworkMessageType.Measurement;
				break;

			case MessageType.Message:
				networkEvent.MessageType = NetworkMessageType.Message;
				break;

			case MessageType.ControlMessage:
				networkEvent.MessageType = NetworkMessageType.ControlMessage;
				break;

			default:
				this.m_logger.LogWarning("Unable to determine message type of type {type}. Integer value: {integerValue}.",
								  message.Type.ToString("G"), message.Type.ToString("D"));
				result = false;
				break;
			}

			return result;
		}
	}
}
