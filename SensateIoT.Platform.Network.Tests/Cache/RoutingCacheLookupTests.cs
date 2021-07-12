/*
 * Unit tests for the data cache.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MongoDB.Bson;
using Moq;

using SensateIoT.Platform.Network.Common.Caching.Abstract;
using SensateIoT.Platform.Network.Common.Caching.Routing;
using SensateIoT.Platform.Network.Data.DTO;

namespace SensateIoT.Platform.Network.Tests.Cache
{
	[TestClass]
	public class RoutingCacheLookupTests
	{
		private ObjectId m_goodSensorID;

		[TestMethod]
		public void CanGetAValidSensor()
		{
			using var cache = this.buildCache();
			Assert.IsNotNull(cache[this.m_goodSensorID]);
		}

		[TestMethod]
		public void CannotLookupWithInvalidID()
		{
			var cache = this.buildCache();
			Assert.ThrowsException<ArgumentException>(() => cache[ObjectId.Empty]);
		}

		[TestMethod]
		public void ReturnsNullWhenNotFound()
		{
			using var cache = this.buildCache();
			Assert.IsNull(cache[ObjectId.GenerateNewId()]);
		}

		private IRoutingCache buildCache()
		{
			var logger = new Mock<ILogger<RoutingCache>>();
			var cache = new RoutingCache(logger.Object);

			for(var idx = 0; idx < 10; idx++) {
				var accountID = Guid.NewGuid();
				var sensorID = ObjectId.GenerateNewId();
				var apiKey = $"key::{idx}";
				this.m_goodSensorID = sensorID;

				var sensor = new Sensor {
					SensorKey = apiKey,
					AccountID = accountID,
					ID = sensorID,
					TriggerInformation = new List<SensorTrigger> {
						new SensorTrigger {
							HasActions = true,
							IsTextTrigger = false
						}
					}
				};

				var account = new Account {
					IsBanned = false,
					HasBillingLockout = false,
					ID = accountID
				};

				var key = new ApiKey {
					IsReadOnly = false,
					IsRevoked = false,
					AccountID = accountID,
				};

				cache[sensor.ID] = sensor;
				cache.Append(account);
				cache.Append(apiKey, key);
			}

			return cache;
		}
	}
}
