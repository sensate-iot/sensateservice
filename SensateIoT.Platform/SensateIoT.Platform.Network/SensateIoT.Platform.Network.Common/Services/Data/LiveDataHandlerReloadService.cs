﻿/*
 * Reload service for API keys.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using SensateIoT.Platform.Network.Common.Caching.Object;
using SensateIoT.Platform.Network.Common.Collections.Remote;
using SensateIoT.Platform.Network.Common.Services.Background;
using SensateIoT.Platform.Network.Common.Settings;
using SensateIoT.Platform.Network.DataAccess.Abstract;

namespace SensateIoT.Platform.Network.Common.Services.Data
{
	public class LiveDataHandlerReloadService : TimedBackgroundService
	{
		private readonly IServiceProvider m_provider;
		private readonly IInternalRemoteQueue m_queue;
		private readonly IDataCache m_cache;
		private readonly ILogger<LiveDataHandlerReloadService> m_logger;

		public LiveDataHandlerReloadService(IServiceProvider provider,
											IInternalRemoteQueue internalRemote,
											IDataCache cache,
											IOptions<DataReloadSettings> settings,
											ILogger<LiveDataHandlerReloadService> logger) : base(settings.Value.StartDelay, settings.Value.DataReloadInterval)
		{
			this.m_provider = provider;
			this.m_queue = internalRemote;
			this.m_logger = logger;
			this.m_cache = cache;
		}

		public override async Task ExecuteAsync(CancellationToken token)
		{
			this.m_logger.LogInformation("Starting live data handler reload at {reloadStart}", DateTime.UtcNow);

			using var scope = this.m_provider.CreateScope();
			var handlerRepo = scope.ServiceProvider.GetRequiredService<ILiveDataHandlerRepository>();

			var sw = Stopwatch.StartNew();
			var rawHandlers = await handlerRepo.GetLiveDataHandlers(token).ConfigureAwait(false);
			var handlers = rawHandlers.ToList();
			this.m_logger.LogInformation("Bulk loaded {count} live data handlers.", handlers.Count);

			this.m_queue.SyncLiveDataHandlers(handlers);
			this.m_cache.SetLiveDataRemotes(handlers);
			sw.Stop();

			this.m_logger.LogInformation("Finished live data handler reload at {reloadEnd}. Reload took {duration}ms.",
										 DateTime.UtcNow, sw.ElapsedMilliseconds);
		}
	}
}