using System;

using SensateIoT.Platform.Network.Common.Routing.Abstract;
using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Abstract;
using SensateIoT.Platform.Network.Data.DTO;

namespace SensateIoT.Platform.Network.Tests.Routing
{
	public class RouterStub : IRouter
	{
		public bool Executed { get; private set; }
		public bool Cancel { get; set; }
		public Exception Exception { get; set; }

		public string Name => "Control Message Router";

		public RouterStub()
		{
			this.Executed = false;
			this.Cancel = false;
			this.Exception = null;
		}

		public bool Route(Sensor sensor, IPlatformMessage message, NetworkEvent networkEvent)
		{
			if(this.Cancel) {
				return false;
			}

			if(this.Exception != null) {
				throw this.Exception;
			}

			this.Executed = true;
			return true;
		}
	}
}