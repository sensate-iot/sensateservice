/*
 * MQTT message service.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

#include <sensateiot/services/messageservice.h>
#include <sensateiot/consumers/commandconsumer.h>

#include <boost/uuid/uuid_io.hpp>
#include <boost/lexical_cast.hpp>
#include <boost/fiber/future/future.hpp>
#include <boost/fiber/future/packaged_task.hpp>

#include <deque>
#include <vector>

#ifdef WIN32
#undef max
#endif

namespace sensateiot::services
{
	namespace detail
	{
		std::size_t WaitForResults(std::vector<boost::fibers::future<MessageService::ProcessingStats>>& results)
		{
			std::size_t authorized = 0UL;
			auto &log = util::Log::GetLog();
			
			try {
				for(auto& future : results) {
					if(!future.valid()) {
						continue;
					}

					authorized += future.get();
				}
			} catch(boost::fibers::future_error& error) {
				log << "Unable to get data from future: " << error.what() << util::Log::NewLine;
			} catch(std::system_error& error) {
				log << "Unable to process messages: " << error.what() << util::Log::NewLine;
			}

			return authorized;
		}
	}
	
	MessageService::MessageService(
			mqtt::IMqttClient &client,
			consumers::CommandConsumer& commands,
			AbstractUserRepository &users,
			AbstractApiKeyRepository &keys,
			AbstractSensorRepository &sensors,
			const config::Config &conf
	) : m_conf(conf), m_measurementIndex(0), m_messageIndex(0),
		m_cache(CacheTimeout), m_lastReload(std::chrono::high_resolution_clock::now()), m_count(0),
	    m_keyRepo(keys), m_userRepo(users), m_sensorRepo(sensors), m_commands(commands)
	{
		std::unique_lock lock(this->m_lock);
		std::string uri = this->m_conf.GetMqtt().GetPrivateBroker().GetBroker().GetUri();

		for(auto idx = 0; idx < this->m_conf.GetWorkers(); idx++) {
			consumers::MeasurementConsumer measurementHandler(client, this->m_cache, conf);
			consumers::MessageConsumer messageHandler(client, this->m_cache, conf);

			this->m_measurementHandlers.emplace_back(std::move(measurementHandler));
			this->m_messageHandlers.emplace_back(std::move(messageHandler));
		}
	}
	
	void MessageService::RawProcess()
	{
		auto &log = util::Log::GetLog();

		std::deque<boost::fibers::packaged_task<ProcessingStats()>> queue;
		std::vector<boost::fibers::future<ProcessingStats>> results;

		std::shared_lock lock(this->m_lock);

		for(auto idx = 0U; idx < this->m_measurementHandlers.size(); idx++) {
			auto processor = [
				&measurementHandler = this->m_measurementHandlers[idx],
				&messageHandler = this->m_messageHandlers[idx],
				&l = this->m_lock
			]() {
				std::size_t result;

				try {
					std::shared_lock lck(l);

					auto messageResult = messageHandler.Process();
					auto measurementResult = measurementHandler.Process();

					result = messageResult + measurementResult;
				} catch(std::exception& ex) {
					util::Log::GetLog() << "Unable to publish messages: " << ex.what() << util::Log::NewLine;
					result = 0;
				}

				return result;
			};
			
			boost::fibers::packaged_task<ProcessingStats()> tsk(std::move(processor));

			results.emplace_back(tsk.get_future());
			queue.emplace_back(std::move(tsk));
		}

		lock.unlock();

		while(!queue.empty()) {
			auto front = std::move(queue.front());
			queue.pop_front();

			std::thread exec(std::move(front));
			exec.detach();
		}

		std::size_t authorized = detail::WaitForResults(results);

		if(authorized != 0ULL) {
			log << "Authorized " << authorized << " messages." << util::Log::NewLine;
		}
	}


	std::time_t MessageService::Process()
	{
		auto &log = util::Log::GetLog();
		auto count = this->m_count.exchange(0ULL);

		auto now = std::chrono::high_resolution_clock::now();
		auto expiry = this->m_lastReload + ReloadTimeout;

		if(expiry <= now) {
			log << "Reloading caches" << util::Log::NewLine;
			this->m_lastReload = now;
			this->LoadAll();
		}

		if (count <= 0) {
			this->m_cache.CleanupFor(CleanupTimeout);
			this->m_commands->Execute();

			return {};
		}

		log << "Processing " << count << " messages!" << util::Log::NewLine;
		auto start = boost::chrono::system_clock::now();
		this->RawProcess();

		this->m_cache.CleanupFor(CleanupTimeout);
		this->m_commands->Execute();

		auto diff = boost::chrono::system_clock::now() - start;
		using Millis = boost::chrono::milliseconds;
		auto duration = boost::chrono::duration_cast<Millis>(diff);

		log << "Processing took: " << duration.count() << "ms." << util::Log::NewLine;

		return duration.count();
	}

	void MessageService::AddMeasurement(std::string msg)
	{
		auto measurement = this->m_validator(msg);

		if(!measurement.first) {
			return;
		}

		this->AddMeasurement(std::make_pair(std::move(msg), std::move(measurement.second)));
	}

	void MessageService::AddMeasurement(std::pair<std::string, models::Measurement> measurement)
	{
		std::shared_lock lock(this->m_lock);
		std::size_t current = this->m_measurementIndex.fetch_add(1);

		current %= this->m_measurementHandlers.size();
		++this->m_count;

		auto &repo = this->m_measurementHandlers[current];
		repo.PushMessage(std::move(measurement));
	}

	void MessageService::AddMessage(std::pair<std::string, models::Message> message)
	{
		std::shared_lock lock(this->m_lock);
		std::size_t current = this->m_messageIndex.fetch_add(1);

		current %= this->m_messageHandlers.size();
		++this->m_count;

		auto &repo = this->m_messageHandlers[current];
		repo.PushMessage(std::move(message));
	}

	void MessageService::AddMeasurements(std::vector<std::pair<std::string, models::Measurement>> measurements)
	{
		if(measurements.size() > std::numeric_limits<unsigned int>::max()) {
			return;
		}
		
		std::shared_lock lock(this->m_lock);
		std::size_t current = this->m_measurementIndex.fetch_add(1);

		current %= this->m_measurementHandlers.size();
		this->m_count += static_cast<unsigned int>(measurements.size());

		auto &repo = this->m_measurementHandlers[current];
		repo.PushMessages(std::move(measurements));
	}

	void MessageService::AddMessages(std::vector<std::pair<std::string, models::Message>> messages)
	{
		if(messages.size() > std::numeric_limits<unsigned int>::max()) {
			return;
		}
		
		std::shared_lock lock(this->m_lock);
		std::size_t current = this->m_messageIndex.fetch_add(1);

		current %= this->m_messageHandlers.size();
		this->m_count += static_cast<unsigned int>(messages.size());

		auto &repo = this->m_messageHandlers[current];
		repo.PushMessages(std::move(messages));
	}

	void MessageService::LoadAll()
	{
		auto sensor_f = std::async(std::launch::async, [this]()
		{
			return this->m_sensorRepo->GetAllSensors(0, 0);
		});

		auto user_f = std::async(std::launch::async, [this]()
		{
			return this->m_userRepo->GetAllUsers();
		});

		auto key_f = std::async(std::launch::async, [this]()
		{
			return this->m_keyRepo->GetAllSensorKeys();
		});

		auto sensors = sensor_f.get();
		auto keys = key_f.get();
		auto users = user_f.get();

		this->m_cache.Append(std::move(sensors));
		this->m_cache.Append(std::move(users));
		this->m_cache.Append(std::move(keys));
	}

	void MessageService::FlushUser(const std::string& id)
	{
		auto userId = boost::lexical_cast<boost::uuids::uuid>(id);
		this->m_cache.FlushUser(userId);
	}

	void MessageService::FlushSensor(const std::string& id)
	{
		models::ObjectId sensorId(id);
		this->m_cache.FlushSensor(sensorId);
	}

	void MessageService::FlushKey(const std::string& key)
	{
		this->m_cache.FlushKey(key);
	}

	void MessageService::AddUser(const std::string& id)
	{
		auto userId = boost::lexical_cast<boost::uuids::uuid>(id);
		auto user = this->m_userRepo->GetUserById(userId);

		if(!user.has_value()) {
			return;
		}

		this->m_cache.Append(std::move(*user));
	}

	void MessageService::AddSensor(const std::string& id)
	{
		models::ObjectId sensorId(id);
		auto sensor = this->m_sensorRepo->GetSensorById(sensorId);

		if(!sensor.has_value()) {
			return;
		}

		this->m_cache.Append(std::move(*sensor));
	}

	void MessageService::AddKey(const std::string& key)
	{
		auto k = this->m_keyRepo->GetSensorKey(key);

		if(!k.has_value()) {
			return;
		}

		this->m_cache.Append(std::move(*k));
	}
}