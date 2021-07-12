/*
 * Remote price update queue.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Options;

using Google.Protobuf;
using Prometheus;

using SensateIoT.Platform.Network.Common.Collections.Abstract;
using SensateIoT.Platform.Network.Common.Collections.Local;
using SensateIoT.Platform.Network.Common.Helpers;
using SensateIoT.Platform.Network.Common.MQTT;
using SensateIoT.Platform.Network.Common.Settings;
using SensateIoT.Platform.Network.Contracts.DTO;

namespace SensateIoT.Platform.Network.Common.Collections.Remote
{
	public class RemoteNetworkEventQueue : IRemoteNetworkEventQueue
	{
		private readonly string m_networkEventQueueTopic;

		private readonly IInternalMqttClient m_client;
		private readonly IQueue<NetworkEvent> m_events;
		private readonly Gauge m_gauge;

		public RemoteNetworkEventQueue(IOptions<QueueSettings> options, IInternalMqttClient client)
		{
			this.m_networkEventQueueTopic = options.Value.NetworkEventQueueTopic;

			this.m_client = client;
			this.m_events = new Deque<NetworkEvent>();
			this.m_gauge = Metrics.CreateGauge("router_events_queued", "Number of messages in the events queue.");
		}

		public void EnqueueEvent(NetworkEvent evt)
		{
			this.m_events.Add(evt);
			this.m_gauge.Inc();
		}

		public async Task FlushEventsAsync()
		{
			this.m_gauge.Set(0D);
			var messages = this.m_events.DequeueRange(int.MaxValue).ToList();

			if(messages.Count <= 0) {
				return;
			}

			var protoEvents = new NetworkEventData();

			foreach(var evt in messages) {
				protoEvents.Events.Add(evt);
			}

			await using var measurementStream = new MemoryStream();
			protoEvents.WriteTo(measurementStream);
			var data = measurementStream.ToArray().Compress();
			await this.m_client.PublishOnAsync(this.m_networkEventQueueTopic, data, false).ConfigureAwait(false);
		}
	}
}
