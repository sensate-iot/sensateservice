/*
 * MQTT message service.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

#pragma once

#include <sensateiot.h>
#include <config/config.h>

#include <boost/uuid/uuid.hpp>
#include <boost/chrono.hpp>

#include <sensateiot/consumers/measurementconsumer.h>
#include <sensateiot/consumers/messageconsumer.h>
#include <sensateiot/mqtt/imqttclient.h>

#include <sensateiot/data/datacache.h>
#include <sensateiot/data/measurementvalidator.h>

#include <sensateiot/services/abstractuserrepository.h>
#include <sensateiot/services/abstractapikeyrepository.h>
#include <sensateiot/services/abstractsensorrepository.h>

#include <sensateiot/models/sensor.h>
#include <sensateiot/models/user.h>
#include <sensateiot/models/apikey.h>
#include <sensateiot/models/objectid.h>
#include <sensateiot/models/measurement.h>
#include <sensateiot/models/message.h>

#include <string>
#include <atomic>
#include <shared_mutex>

namespace sensateiot::consumers
{
	class CommandConsumer;
}

namespace sensateiot::services
{
	class MessageService {
	public:
		typedef std::size_t ProcessingStats;

		explicit MessageService(mqtt::IMqttClient& client,
								consumers::CommandConsumer& commands,
		                        AbstractUserRepository& users,
		                        AbstractApiKeyRepository& keys,
		                        AbstractSensorRepository& sensors,
		                        const config::Config& conf);

		std::time_t Process();
		void AddMeasurement(std::string msg);
		void AddMeasurement(std::pair<std::string, models::Measurement> measurement);
		void AddMessage(std::pair<std::string, models::Message> messages);
		void AddMeasurements(std::vector<std::pair<std::string, models::Measurement>> measurements);
		void AddMessages(std::vector<std::pair<std::string, models::Message>> messages);
		void LoadAll();

		void FlushUser(const std::string& id);
		void FlushSensor(const std::string& id);
		void FlushKey(const std::string& key);
		void AddUser(const std::string& id);
		void AddSensor(const std::string& id);
		void AddKey(const std::string& key);

	private:
		mutable std::shared_mutex m_lock;
		config::Config m_conf;
		std::atomic_uint8_t m_measurementIndex;
		std::atomic_uint8_t m_messageIndex;
		std::vector<consumers::MeasurementConsumer> m_measurementHandlers;
		std::vector<consumers::MessageConsumer> m_messageHandlers;

		data::DataCache m_cache;
		std::chrono::high_resolution_clock::time_point m_lastReload;
		data::MeasurementValidator m_validator;
		std::atomic_uint m_count;

		stl::ReferenceWrapper<AbstractApiKeyRepository> m_keyRepo;
		stl::ReferenceWrapper<AbstractUserRepository> m_userRepo;
		stl::ReferenceWrapper<AbstractSensorRepository> m_sensorRepo;
		stl::ReferenceWrapper<consumers::CommandConsumer> m_commands;

		void RawProcess();

		static constexpr int Increment = 1;
		static constexpr auto CleanupTimeout = std::chrono::milliseconds(25);
		static constexpr auto CacheTimeout   = std::chrono::minutes(6);
		static constexpr auto ReloadTimeout  = std::chrono::minutes(5);
	};
}