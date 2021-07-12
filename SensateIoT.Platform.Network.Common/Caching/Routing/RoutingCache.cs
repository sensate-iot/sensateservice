/*
 * In-memory routing cache implementation.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using JetBrains.Annotations;
using MongoDB.Bson;

using SensateIoT.Platform.Network.Common.Caching.Abstract;
using SensateIoT.Platform.Network.Common.Caching.Comparators;
using SensateIoT.Platform.Network.Data.DTO;
using SensateIoT.Platform.Network.Data.Models;

using ApiKey = SensateIoT.Platform.Network.Data.DTO.ApiKey;
using Sensor = SensateIoT.Platform.Network.Data.DTO.Sensor;

namespace SensateIoT.Platform.Network.Common.Caching.Routing
{
	[UsedImplicitly]
	public sealed class RoutingCache : IRoutingCache
	{
		private readonly ReaderWriterLockSlim m_lock;
		private readonly IDictionary<ObjectId, Sensor> m_sensors;
		private readonly IDictionary<Guid, Account> m_accounts;
		private readonly IDictionary<string, ApiKey> m_sensorKeys;
		private readonly IDictionary<string, LiveDataHandler> m_remotes;
		private HashSet<ObjectId> m_liveSensors;
		private HashSet<LiveDataRoute> m_liveDataEntries;
		private HashSet<LiveDataRoute> m_liveDataSyncEntries;

		private bool m_disposed;
		private readonly ILogger<RoutingCache> m_logger;

		public RoutingCache(ILogger<RoutingCache> logger)
		{
			this.m_disposed = false;
			this.m_logger = logger;
			this.m_lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
			this.m_sensors = new Dictionary<ObjectId, Sensor>();
			this.m_sensorKeys = new Dictionary<string, ApiKey>();
			this.m_accounts = new Dictionary<Guid, Account>();

			this.m_liveSensors = new HashSet<ObjectId>();
			this.m_liveDataSyncEntries = new HashSet<LiveDataRoute>(new LiveDataRouteEqualityComparer());
			this.m_liveDataEntries = new HashSet<LiveDataRoute>(new LiveDataRouteEqualityComparer());
			this.m_remotes = new ConcurrentDictionary<string, LiveDataHandler>();
		}

		public Sensor this[ObjectId id] {
			get => this.LookupSensor(id);
			set => this.InsertSensor(id, value);
		}

		public Account GetAccount(Guid id)
		{
			Account result;

			this.CheckDisposed();
			this.m_lock.EnterReadLock();

			try {
				if(!this.m_accounts.TryGetValue(id, out result)) {
					return null;
				}
			} finally {
				this.m_lock.ExitReadLock();
			}

			return result;
		}

		public ApiKey GetApiKey(string key)
		{
			ApiKey result;

			this.CheckDisposed();
			this.m_lock.EnterReadLock();

			try {
				if(!this.m_sensorKeys.TryGetValue(key, out result)) {
					return null;
				}
			} finally {
				this.m_lock.ExitReadLock();
			}

			return result;
		}

		public void Load(IEnumerable<Sensor> sensors)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.InternalLoad(sensors);
			} finally {
				this.m_lock.ExitWriteLock();
			}

			GarbageCollection.Collect();
		}

		private void InternalLoad(IEnumerable<Sensor> sensors)
		{
			this.m_sensors.Clear();

			foreach(var sensor in sensors) {
				this.m_sensors[sensor.ID] = sensor;
			}

			foreach(var entry in this.m_liveDataEntries) {
				this.InternalLoad(entry);
			}
		}

		private void InternalLoad(LiveDataRoute entry)
		{
			if(!this.m_sensors.TryGetValue(entry.SensorID, out var sensor)) {
				return;
			}

			sensor.LiveDataRouting ??= new HashSet<RoutingTarget>(new RoutingTargetEqualityComparer());
			sensor.LiveDataRouting.Add(new RoutingTarget {
				Target = entry.Target,
				Type = RouteType.LiveDataSubscription
			});
		}

		public void Load(IEnumerable<Account> accounts)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_accounts.Clear();

				foreach(var account in accounts) {
					this.m_accounts[account.ID] = account;
				}
			} finally {
				this.m_lock.ExitWriteLock();
			}

			GarbageCollection.Collect();
		}

		public void Load(IEnumerable<Tuple<string, ApiKey>> keys)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_sensorKeys.Clear();

				foreach(var (id, apikey) in keys) {
					this.m_sensorKeys[id] = apikey;
				}
			} finally {
				this.m_lock.ExitWriteLock();
			}

			GarbageCollection.Collect();
		}

		public void Append(Account account)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_accounts[account.ID] = account;
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		public void Append(string key, ApiKey apikey)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_sensorKeys[key] = apikey;
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		public void RemoveSensor(ObjectId id)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_sensors.Remove(id);
				this.m_liveSensors.Remove(id);
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		public void RemoveAccount(Guid id)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_accounts.Remove(id);
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		public void RemoveApiKey(string key)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_sensorKeys.Remove(key);
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		public void AddLiveDataRoute(LiveDataRoute route)
		{
			this.CheckDisposed();

			if(!this.m_remotes.TryGetValue(route.Target, out var remote) || !remote.Enabled) {
				return;
			}

			this.AddLiveDataRouting(route);
		}

		public void RemoveLiveDataRoute(LiveDataRoute route)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				var sensor = this.m_sensors[route.SensorID];
				sensor.LiveDataRouting?.Remove(new RoutingTarget { Target = route.Target, Type = RouteType.LiveDataSubscription });

				this.m_liveDataEntries.Remove(route);
				this.m_liveDataSyncEntries.Remove(route);

				if(sensor.LiveDataRouting != null && sensor.LiveDataRouting!.All(x => x.Type != RouteType.LiveDataSubscription)) {
					this.m_liveSensors.Remove(route.SensorID);
				}
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		public void SyncLiveDataRoutes(ICollection<LiveDataRoute> data)
		{
			this.CheckDisposed();
			var list = data.ToList();

			list.RemoveAll(x => !this.m_remotes.TryGetValue(x.Target, out var value) || !value.Enabled);
			this.SyncLiveDataRoutes((IEnumerable<LiveDataRoute>)list);
		}

		public void SetLiveDataRemotes(IEnumerable<LiveDataHandler> remotes)
		{
			this.CheckDisposed();

			foreach(var liveDataHandler in remotes) {
				this.m_remotes[liveDataHandler.Name] = liveDataHandler;
			}
		}

		public void FlushLiveDataRoutes()
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.InternalFlushRoutes();
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		public void Flush()
		{
			GarbageCollection.Collect();
		}

		private void InternalFlushRoutes()
		{
			this.m_liveSensors = new HashSet<ObjectId>();

			this.m_liveDataEntries = this.m_liveDataSyncEntries;
			this.m_liveDataSyncEntries = new HashSet<LiveDataRoute>(new LiveDataRouteEqualityComparer());

			foreach(var entry in this.m_liveDataEntries) {
				this.FlushLiveDataRoute(entry);
			}
		}

		private void FlushLiveDataRoute(LiveDataRoute entry)
		{
			if(!this.m_sensors.TryGetValue(entry.SensorID, out var sensor)) {
				return;
			}

			if(this.m_liveSensors.Add(entry.SensorID)) {
				sensor.LiveDataRouting ??= new HashSet<RoutingTarget>(new RoutingTargetEqualityComparer());
				sensor.LiveDataRouting.RemoveWhere(x => x.Type == RouteType.LiveDataSubscription);
			}

			sensor.LiveDataRouting.Add(new RoutingTarget {
				Target = entry.Target,
				Type = RouteType.LiveDataSubscription
			});
		}

		private void InsertSensor(ObjectId id, Sensor sensor)
		{
			this.CheckDisposed();
			VerifyObjectId(id);

			if(id != sensor.ID) {
				throw new ArgumentException($"ID ({id}) is not equal to encapsulated ID ({sensor.ID}).", nameof(id));
			}

			this.m_lock.EnterWriteLock();

			try {
				this.InternalInsertSensor(id, sensor);
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		private void InternalInsertSensor(ObjectId id, Sensor sensor)
		{
			this.m_sensors[sensor.ID] = sensor;

			foreach(var entry in this.m_liveDataEntries) {
				if(entry.SensorID != id || !this.m_sensors.TryGetValue(entry.SensorID, out var value)) {
					continue;
				}

				value.LiveDataRouting ??= new HashSet<RoutingTarget>(new RoutingTargetEqualityComparer());
				value.LiveDataRouting.Add(new RoutingTarget {
					Target = entry.Target,
					Type = RouteType.LiveDataSubscription
				});
			}
		}

		private Sensor LookupSensor(ObjectId id)
		{
			Sensor result;

			this.CheckDisposed();
			VerifyObjectId(id);
			this.m_lock.EnterReadLock();

			try {
				if(!this.m_sensors.TryGetValue(id, out result)) {
					this.m_logger.LogWarning("Unable to find sensor with ID: {sensorId}.", id.ToString());
					return null;
				}
			} catch(KeyNotFoundException ex) {
				this.m_logger.LogWarning(ex, "Unable to find account or key for sensor {sensorId}.", id.ToString());
				result = null;
			} finally {
				this.m_lock.ExitReadLock();
			}

			return result;
		}

		private void AddLiveDataRouting(LiveDataRoute route)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_liveSensors.Add(route.SensorID);
				this.m_liveDataEntries.Add(route);
				this.m_liveDataSyncEntries.Add(route);

				var sensor = this.m_sensors[route.SensorID];

				sensor.LiveDataRouting ??= new HashSet<RoutingTarget>(new RoutingTargetEqualityComparer());
				sensor.LiveDataRouting.Add(new RoutingTarget { Target = route.Target, Type = RouteType.LiveDataSubscription });
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		private void SyncLiveDataRoutes(IEnumerable<LiveDataRoute> data)
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_liveDataSyncEntries.UnionWith(data);
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		private void Clear()
		{
			this.CheckDisposed();
			this.m_lock.EnterWriteLock();

			try {
				this.m_sensors.Clear();
				this.m_accounts.Clear();
				this.m_sensorKeys.Clear();
			} finally {
				this.m_lock.ExitWriteLock();
			}
		}

		[AssertionMethod]
		private static void VerifyObjectId(ObjectId id)
		{
			if(id == ObjectId.Empty) {
				throw new ArgumentException($"Invalid object ID: {id}", nameof(id));
			}
		}

		[AssertionMethod]
		private void CheckDisposed()
		{
			if(!this.m_disposed) {
				return;
			}

			throw new ObjectDisposedException(nameof(RoutingCache));
		}

		public void Dispose()
		{
			if(this.m_disposed) {
				return;
			}

			this.Clear();
			this.m_lock.Dispose();
			this.m_disposed = true;
		}
	}
}
