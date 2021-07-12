/*
 * Message router implementation.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf;
using Prometheus;

using Microsoft.Extensions.Logging;

using SensateIoT.Platform.Network.Common.Caching.Abstract;
using SensateIoT.Platform.Network.Common.Collections.Abstract;
using SensateIoT.Platform.Network.Common.Exceptions;
using SensateIoT.Platform.Network.Common.Routing.Abstract;
using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Abstract;
using SensateIoT.Platform.Network.Data.DTO;

namespace SensateIoT.Platform.Network.Common.Routing
{
	public sealed class CompositeRouter : IMessageRouter
	{
		private readonly IList<IRouter> m_routers;
		private readonly ReaderWriterLockSlim m_lock;
		private readonly IRoutingCache m_cache;
		private readonly IRemoteNetworkEventQueue m_eventQueue;
		private readonly ILogger<CompositeRouter> m_logger;
		private readonly Counter m_counter;
		private readonly Counter m_dropCounter;
		private readonly Histogram m_duration;

		private bool m_disposed;

		public CompositeRouter(IRoutingCache cache, IRemoteNetworkEventQueue queue, ILogger<CompositeRouter> logger)
		{
			this.m_routers = new List<IRouter>();
			this.m_lock = new ReaderWriterLockSlim();
			this.m_cache = cache;
			this.m_eventQueue = queue;
			this.m_logger = logger;
			this.m_disposed = false;
			this.m_dropCounter = Metrics.CreateCounter("router_messages_dropped_total", "Total number of measurements/messages dropped.");
			this.m_counter = Metrics.CreateCounter("router_messages_routed_total", "Total number of measurements/messages routed.");
			this.m_duration = Metrics.CreateHistogram("router_duration_seconds", "Message routing duration.");
		}

		public void AddRouter(IRouter router)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_routers.Add(router);
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		public void Route(IEnumerable<IPlatformMessage> messages)
		{
			this.CheckDisposed();
			this.m_lock.EnterReadLock();

			var messageList = messages.ToList();

			try {
				var result = Parallel.ForEach(messageList, this.InternalRoute);

				if(!result.IsCompleted) {
					this.m_logger.LogError("Unable to complete routing {count} messages", messageList.Count);
				}
			} catch(AggregateException ex) {
				throw ex.InnerException!;
			} finally {
				this.m_lock.ExitReadLock();
			}
		}

		private void InternalRoute(IPlatformMessage message)
		{
			using(this.m_duration.NewTimer()) {
				var sensor = this.m_cache[message.SensorID];
				this.ProcessMessage(sensor, message);
			}
		}

		private void ProcessMessage(Sensor sensor, IPlatformMessage message)
		{
			if(sensor == null) {
				this.m_dropCounter.Inc();
				this.m_logger.LogDebug("Unable to route message for sensor: {sensorId}. Sensor not found", message.SensorID.ToString());
				return;
			}

			this.m_logger.LogDebug("Routing message of type {type} for sensor {sensorId}", message.Type.ToString("G"), sensor.ID.ToString());
			this.RouteMessage(message, sensor);
		}

		private bool IsValidSensor(Sensor sensor, Account account, ApiKey key)
		{
			bool invalid;

			invalid = this.ValidateAccount(sensor, account, key);

			invalid |= key.IsReadOnly;
			invalid |= key.IsRevoked;

			return !invalid;
		}

		private bool ValidateAccount(Sensor sensor, Account account, ApiKey key)
		{
			var invalid = false;

			if(account.HasBillingLockout) {
				this.m_logger.LogInformation("Skipping sensor {sensorId} due to billing lock", sensor.ID.ToString());
				invalid = true;
			}

			if(account.IsBanned) {
				this.m_logger.LogInformation("Skipping sensor because account {accountId:D} is banned", account.ID);
				invalid = true;
			}

			invalid |= account.ID != key.AccountID;
			return invalid;
		}


		private void RouteMessage(IPlatformMessage message, Sensor sensor)
		{
			var @event = CreateNetworkEvent(sensor);

			this.m_counter.Inc();
			message.PlatformTimestamp = DateTime.UtcNow;

			if(!this.VerifySensor(sensor)) {
				this.m_dropCounter.Inc();
				this.m_eventQueue.EnqueueEvent(@event);
				return;
			}

			foreach(var router in this.m_routers) {
				bool status;

				try {
					status = router.Route(sensor, message, @event);
				} catch(RouterException ex) {
					status = false;
					this.m_dropCounter.Inc();
					this.m_logger.LogWarning(ex, "Unable to route message for sensor {sensorId}", message.SensorID.ToString());
				}

				if(!status) {
					this.m_logger.LogDebug("Routing cancelled by the {routerName}", router.Name);
					break;
				}
			}

			this.m_eventQueue.EnqueueEvent(@event);
		}

		private bool VerifySensor(Sensor sensor)
		{
			var account = this.m_cache.GetAccount(sensor.AccountID);
			var key = this.m_cache.GetApiKey(sensor.SensorKey);

			if(account == null) {
				this.m_logger.LogWarning("Account with ID {accountId:D} not found", sensor.AccountID);
				return false;
			}

			if(key == null) {
				this.m_logger.LogWarning("API key for sensor with ID {sensorId} not found", sensor.ID);
				return false;
			}

			return this.IsValidSensor(sensor, account, key);
		}

		private static NetworkEvent CreateNetworkEvent(Sensor sensor)
		{
			var evt = new NetworkEvent {
				SensorID = ByteString.CopyFrom(sensor.ID.ToByteArray()),
				AccountID = ByteString.CopyFrom(sensor.AccountID.ToByteArray())
			};

			evt.Actions.Add(NetworkEventType.MessageRouted);
			return evt;
		}

		private void CheckDisposed()
		{
			if(!this.m_disposed) {
				return;
			}

			throw new ObjectDisposedException(nameof(CompositeRouter));
		}

		public void Dispose()
		{
			this.m_disposed = true;
			this.m_lock.Dispose();
		}
	}
}
