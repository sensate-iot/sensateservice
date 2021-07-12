/*
 * Composite routing interface.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Abstract;
using SensateIoT.Platform.Network.Data.DTO;

namespace SensateIoT.Platform.Network.Common.Routing.Abstract
{
	public interface IRouter
	{
		public string Name { get; }
		bool Route(Sensor sensor, IPlatformMessage message, NetworkEvent networkEvent);
	}
}
