﻿/*
 * Program startup.
 *
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

using System;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SensateService.Config;
using SensateService.Infrastructure.Sql;
using SensateService.Init;
using SensateService.Models;
using SensateService.Services;
using SensateService.Services.Adapters;
using SensateService.Services.Settings;
using SensateService.TriggerHandler.Models;
using SensateService.TriggerHandler.Mqtt;
using SensateService.TriggerHandler.Services;

namespace SensateService.TriggerHandler.Application
{
	public class Startup
	{
		private readonly IConfiguration Configuration;

		public Startup(IConfiguration configuration)
		{
			this.Configuration = configuration;
		}

		private static bool IsDevelopment()
		{
#if DEBUG
			var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
			return env == "Development";
#else
			return false;
#endif

		}

		private void SetupCommunicationChannels(IServiceCollection services)
		{
			var mail = new MailConfig();
			var text = new TextConfig();

			this.Configuration.GetSection("Mail").Bind(mail);
			this.Configuration.GetSection("Text").Bind(text);

			if(mail.Provider == "SendGrid") {
				services.AddScoped<IEmailSender, SendGridMailer>();
				services.Configure<SendGridAuthOptions>(opts => {
					opts.FromName = mail.FromName;
					opts.From = mail.From;
					opts.Key = mail.SendGrid.Key;
					opts.Username = mail.SendGrid.Username;
				});
			} else if(mail.Provider == "SMTP") {
				services.AddScoped<IEmailSender, SmtpMailer>();
				services.Configure<SmtpAuthOptions>(opts => {
					opts.FromName = mail.FromName;
					opts.From = mail.From;
					opts.Password = mail.Smtp.Password;
					opts.Username = mail.Smtp.Username;
					opts.Ssl = mail.Smtp.Ssl;
					opts.Port = mail.Smtp.Port;
					opts.Host = mail.Smtp.Host;
				});
			}

			if(text.Provider == "Twillio") {
				services.AddTwilioTextApi(text);
			} else {
				Console.WriteLine("Text message provider not configured!");
			}
		}

		public void ConfigureServices(IServiceCollection services)
		{
			var mqtt = new MqttConfig();
			var db = new DatabaseConfig();
			var cache = new CacheConfig();
			var timeouts = new TimeoutSettings();

			this.Configuration.GetSection("Mqtt").Bind(mqtt);
			this.Configuration.GetSection("Database").Bind(db);
			this.Configuration.GetSection("Cache").Bind(cache);
			this.Configuration.GetSection("Timeouts").Bind(timeouts);

			var privatemqtt = mqtt.InternalBroker;
			var publicmqtt = mqtt.PublicBroker;

			services.AddPostgres(db.PgSQL.ConnectionString);

			services.AddIdentity<SensateUser, SensateRole>(config => {
				config.SignIn.RequireConfirmedEmail = true;
			})
			.AddEntityFrameworkStores<SensateSqlContext>()
			.AddDefaultTokenProviders();

			services.AddLogging(builder => { builder.AddConfiguration(this.Configuration.GetSection("Logging")); });

			if(cache.Enabled) {
				services.AddCacheStrategy(cache, db);
			}

			services.AddDocumentStore(db.MongoDB.ConnectionString, db.MongoDB.DatabaseName, db.MongoDB.MaxConnections);
			services.AddDocumentRepositories(cache.Enabled);
			services.AddSqlRepositories(cache.Enabled);
			services.AddMeasurementStorage(cache);
			services.AddHashAlgorihms();

			this.SetupCommunicationChannels(services);

			services.AddScoped<ITriggerNumberMatchingService, TriggerNumberMatchingService>();
			services.AddScoped<ITriggerHandlerService, TriggerHandlerService>();

			services.AddMqttService(options => {
				options.Ssl = privatemqtt.Ssl;
				options.Host = privatemqtt.Host;
				options.Port = privatemqtt.Port;
				options.Username = privatemqtt.Username;
				options.Password = privatemqtt.Password;
				options.Id = Guid.NewGuid().ToString();
			});

			services.AddMqttPublishService<MqttPublishService>(options => {
				options.Ssl = publicmqtt.Ssl;
				options.Host = publicmqtt.Host;
				options.Port = publicmqtt.Port;
				options.Username = publicmqtt.Username;
				options.Password = publicmqtt.Password;
				options.ActuatorTopic = publicmqtt.ActuatorTopic;
				options.Id = Guid.NewGuid().ToString();
			});

			services.AddLogging(builder => {
				builder.AddConfiguration(Configuration.GetSection("Logging"));
				if(IsDevelopment())
					builder.AddDebug();

				builder.AddConsole();
			});

			/* Configure the timeout in minutes */
			services.Configure<TimeoutSettings>(options => {
				options.MailTimeout = timeouts.MailTimeout;
				options.MessageTimeout = timeouts.MessageTimeout;
				options.MqttTimeout = timeouts.MqttTimeout;
				options.HttpTimeout = timeouts.HttpTimeout;
			});
		}

		public void Configure(IServiceProvider provider)
		{
			var mqtt = new MqttConfig();
			var cache = new CacheConfig();

			this.Configuration.GetSection("Mqtt").Bind(mqtt);
			this.Configuration.GetSection("Cache").Bind(cache);
			var @private = mqtt.InternalBroker;

			provider.MapInternalMqttTopic<MqttBulkNumberTriggerHandler>(@private.InternalBulkMeasurementTopic);
			provider.MapInternalMqttTopic<MqttNumberTriggerHandler>(@private.InternalMeasurementTopic);
			provider.MapInternalMqttTopic<MqttFormalLanguageTriggerHandler>(@private.InternalMessageTopic);
		}
	}
}

