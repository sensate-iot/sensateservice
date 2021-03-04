﻿/*
 * MQTT handler for incoming messages.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using JetBrains.Annotations;
using Prometheus;

using SensateIoT.Platform.Network.Common.Converters;
using SensateIoT.Platform.Network.Common.MQTT;
using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Models;
using SensateIoT.Platform.Network.DataAccess.Abstract;

namespace SensateIoT.Platform.Network.StorageService.MQTT
{
	[UsedImplicitly]
	public class MqttBulkMessageHandler : IMqttHandler
	{
		private readonly ILogger<MqttBulkMessageHandler> m_logger;
		private readonly IMessageRepository m_messages;
		private readonly Counter m_storageCounter;
		private readonly Histogram m_duration;

		public MqttBulkMessageHandler(IMessageRepository message, ILogger<MqttBulkMessageHandler> logger)
		{
			this.m_logger = logger;
			this.m_messages = message;
			this.m_storageCounter = Metrics.CreateCounter("storageservice_messages_stored_total", "Total number of messages stored.");
			this.m_duration = Metrics.CreateHistogram("storageservice_message_storage_duration_seconds", "Histogram of message storage duration.");
		}

		public async Task OnMessageAsync(string topic, string message, CancellationToken ct)
		{
			var sw = Stopwatch.StartNew();

			try {
				using(this.m_duration.NewTimer()) {
					var databaseMessages = this.Decompress(message).ToList();

					this.m_storageCounter.Inc(databaseMessages.Count);
					await this.m_messages.CreateRangeAsync(databaseMessages, ct).ConfigureAwait(false);
				}

			} catch(Exception ex) {
				this.m_logger.LogWarning("Unable to store message: {exception} " +
										 "Message content: {message}. " +
										 "Stack trace: ", ex.Message, message, ex.StackTrace);
			}

			sw.Stop();
			this.m_logger.LogInformation("Storage attempt of messages took {timespan}.", sw.Elapsed.ToString("c"));
		}

		private IEnumerable<Message> Decompress(string data)
		{
			var bytes = Convert.FromBase64String(data);
			using var to = new MemoryStream();
			using var from = new MemoryStream(bytes);
			using var gzip = new GZipStream(@from, CompressionMode.Decompress);

			gzip.CopyTo(to);
			var final = to.ToArray();
			var protoMeasurements = TextMessageData.Parser.ParseFrom(final);
			this.m_logger.LogInformation("Storing {count} messages!", protoMeasurements.Messages.Count);
			return MessageDatabaseConverter.Convert(protoMeasurements);
		}
	}
}