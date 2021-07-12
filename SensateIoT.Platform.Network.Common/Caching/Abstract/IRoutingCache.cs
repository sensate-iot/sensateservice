/*
 * Routing cache interface.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Collections.Generic;

using MongoDB.Bson;

using SensateIoT.Platform.Network.Data.DTO;
using SensateIoT.Platform.Network.Data.Models;

using ApiKey = SensateIoT.Platform.Network.Data.DTO.ApiKey;
using Sensor = SensateIoT.Platform.Network.Data.DTO.Sensor;

namespace SensateIoT.Platform.Network.Common.Caching.Abstract
{
	public interface IRoutingCache : IDisposable
	{
		Sensor this[ObjectId id] { get; set; }
		Account GetAccount(Guid id);
		ApiKey GetApiKey(string key);

		void Load(IEnumerable<Sensor> sensors);
		void Load(IEnumerable<Account> accounts);
		void Load(IEnumerable<Tuple<string, ApiKey>> keys);
		void Append(Account account);
		void Append(string key, ApiKey apikey);

		void RemoveSensor(ObjectId id);
		void RemoveAccount(Guid id);
		void RemoveApiKey(string key);

		void AddLiveDataRoute(LiveDataRoute route);
		void RemoveLiveDataRoute(LiveDataRoute route);
		void SyncLiveDataRoutes(ICollection<LiveDataRoute> data);
		void SetLiveDataRemotes(IEnumerable<LiveDataHandler> remotes);
		void FlushLiveDataRoutes();

		void Flush();
	}
}
