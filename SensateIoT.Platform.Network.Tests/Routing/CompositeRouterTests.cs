/*
 * Composite router unit tests.
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
using SensateIoT.Platform.Network.Common.Collections.Abstract;
using SensateIoT.Platform.Network.Common.Exceptions;
using SensateIoT.Platform.Network.Common.Routing;
using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Abstract;
using SensateIoT.Platform.Network.Data.DTO;

namespace SensateIoT.Platform.Network.Tests.Routing
{
	[TestClass]
	public class CompositeRouterTests
	{
		private static readonly Sensor Sensor = new Sensor { ID = ObjectId.GenerateNewId(), AccountID = Guid.NewGuid(), SensorKey = "Abcd" };
		private static readonly Account Account = new Account { ID = Sensor.AccountID };
		private static readonly ApiKey ApiKey = new ApiKey { AccountID = Sensor.AccountID };

		[TestMethod]
		public void CanExecuteRouters()
		{
			var router = CreateCompositeRouter();

			var r1 = new RouterStub();
			var r2 = new RouterStub();
			var r3 = new RouterStub();

			router.AddRouter(r1);
			router.AddRouter(r2);
			router.AddRouter(r3);

			var msg = new Message {
				SensorId = Sensor.ID
			};
			router.Route(AsList(msg));

			Assert.IsTrue(r1.Executed);
			Assert.IsTrue(r2.Executed);
			Assert.IsTrue(r3.Executed);
		}

		[TestMethod]
		public void CannotRouteFromBannedAccount()
		{
		}

		[TestMethod]
		public void CannotRouteBillingLockedAccount()
		{
		}

		[TestMethod]
		public void CannotRouteWithReadOnlyKey()
		{

		}

		[TestMethod]
		public void CannotRouteWithRevokedKey()
		{

		}

		[TestMethod]
		public void CannotRouteWithoutAccount()
		{
		}

		[TestMethod]
		public void CannotRouteWithoutSensorKey()
		{
		}

		[TestMethod]
		public void SensorCannotBeNull()
		{
			var router = CreateCompositeRouter();

			var r1 = new RouterStub();
			var r2 = new RouterStub();
			var r3 = new RouterStub();

			router.AddRouter(r1);
			router.AddRouter(r2);
			router.AddRouter(r3);

			var msg = new Message {
				SensorId = ObjectId.GenerateNewId()
			};

			router.Route(AsList(msg));

			Assert.IsFalse(r1.Executed);
			Assert.IsFalse(r2.Executed);
			Assert.IsFalse(r3.Executed);
		}

		[TestMethod]
		public void CanCancelRoutesGracefully()
		{
			var router = CreateCompositeRouter();

			var r1 = new RouterStub();
			var r2 = new RouterStub { Cancel = true };
			var r3 = new RouterStub();

			router.AddRouter(r1);
			router.AddRouter(r2);
			router.AddRouter(r3);

			var msg = new Message {
				SensorId = Sensor.ID
			};
			router.Route(AsList(msg));

			Assert.IsTrue(r1.Executed);
			Assert.IsFalse(r2.Executed);
			Assert.IsFalse(r3.Executed);
		}

		[TestMethod]
		public void CanCatchRouterExceptions()
		{
			var router = CreateCompositeRouter();

			var r1 = new RouterStub();
			var r2 = new RouterStub { Exception = new RouterException("TestRouter", "testing exception catching.") };
			var r3 = new RouterStub();

			router.AddRouter(r1);
			router.AddRouter(r2);
			router.AddRouter(r3);

			var msg = new Message {
				SensorId = Sensor.ID
			};
			router.Route(AsList(msg));

			Assert.IsTrue(r1.Executed);
			Assert.IsFalse(r2.Executed);
			Assert.IsFalse(r3.Executed);
		}

		[TestMethod]
		public void CannotCatchOtherExceptions()
		{
			var router = CreateCompositeRouter();

			var r1 = new RouterStub();
			var r2 = new RouterStub { Exception = new InvalidOperationException() };
			var r3 = new RouterStub();

			router.AddRouter(r1);
			router.AddRouter(r2);
			router.AddRouter(r3);

			var msg = new Message {
				SensorId = Sensor.ID
			};

			Assert.ThrowsException<InvalidOperationException>(() => router.Route(AsList(msg)));
		}

		[TestMethod]
		public void EnqueuesNetworkEvents()
		{
			var count = 0;
			var logger = new Mock<ILogger<CompositeRouter>>();
			var queue = new Mock<IRemoteNetworkEventQueue>();

			queue.Setup(x => x.EnqueueEvent(It.IsAny<NetworkEvent>())).Callback(() => count += 1);
			var router = new CompositeRouter(CreateRoutingCache(), queue.Object, logger.Object);

			var r1 = new RouterStub();
			var r2 = new RouterStub();

			router.AddRouter(r1);
			router.AddRouter(r2);

			var msg = new Message {
				SensorId = Sensor.ID
			};

			router.Route(AsList(msg));
			Assert.AreEqual(1, count);
		}

		private static IRoutingCache CreateRoutingCache()
		{
			var cache = new Mock<IRoutingCache>();

			cache.Setup(x => x[Sensor.ID]).Returns(Sensor);
			cache.Setup(x => x.GetAccount(Account.ID)).Returns(Account);
			cache.Setup(x => x.GetApiKey(Sensor.SensorKey)).Returns(ApiKey);
			return cache.Object;
		}

		private static CompositeRouter CreateCompositeRouter()
		{
			var logger = new Mock<ILogger<CompositeRouter>>();
			var queue = new Mock<IRemoteNetworkEventQueue>();

			return new CompositeRouter(CreateRoutingCache(), queue.Object, logger.Object);
		}

		private static IList<IPlatformMessage> AsList(IPlatformMessage message)
		{
			return new List<IPlatformMessage> { message };
		}
	}
}
