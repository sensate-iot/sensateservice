/*
 * Message data consumer.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

#include <sensateiot/consumers/messageconsumer.h>
#include <sensateiot/util/sha256.h>
#include <sensateiot/util/protobuf.h>

#include <boost/chrono/chrono.hpp>
#include <re2/re2.h>

#include <string>
#include <string_view>
#include <vector>
#include <mutex>

namespace sensateiot::consumers
{
	MessageConsumer::MessageConsumer(mqtt::IMqttClient& client, data::DataCache& cache, config::Config conf) :
		AbstractConsumer(client, cache, std::move(conf))
	{
	}

	MessageConsumer::MessageConsumer(MessageConsumer&& rhs) noexcept :
		AbstractConsumer(std::forward<AbstractConsumer>(rhs))
	{
	}

	MessageConsumer& MessageConsumer::operator=(MessageConsumer&& rhs) noexcept
	{
		std::scoped_lock l(this->m_lock, rhs.m_lock);
		this->Move(rhs);

		return *this;
	}

	AbstractConsumer<models::Message>::ProcessingStats MessageConsumer::Process()
	{
		std::vector<MessagePair> data;
		SensorLookupType sensor;

		this->m_lock.lock();
		data.reserve(MessageArraySize);
		std::swap(this->m_messages, data);
		this->m_messages.clear();
		this->m_lock.unlock();

		std::sort(std::begin(data), std::end(data), [](const MessagePair& x, const MessagePair& y)
		{
			return x.second.GetObjectId().compare(y.second.GetObjectId()) < 0;
		});
		
		std::vector<models::Message> authorized;
		authorized.reserve(data.size());
		auto now = std::chrono::high_resolution_clock::now();

		for(auto&& pair : data) {
			if(!sensor.second.has_value() || sensor.second->GetId() != pair.second.GetObjectId()) {
				sensor = this->m_cache->GetSensor(pair.second.GetObjectId(), now);
			}

			if(!sensor.first) {
				continue;
			}

			if(!sensor.second.has_value()) {
				/* Found but not valid, exit */
				continue;
			}

			/* Valid sensor, validate the measurement */
			if(!this->ValidateMessage(sensor.second.value(), pair)) {
				continue;
			}

			authorized.emplace_back(std::move(pair.second));
		}

		data.clear();

		if(!authorized.empty()) {
			this->PublishAuthorizedMessages(authorized, this->m_config.GetMqtt().GetPrivateBroker().GetBulkMessageTopic());
		}

		return authorized.size();
	}

	bool MessageConsumer::ValidateMessage(const models::Sensor& sensor, MessagePair& pair) const
	{
		auto result = RE2::Replace(&pair.first, this->m_regex, sensor.GetSecret());

		if(result) {
			auto offset = pair.second.GetSecret().length() - SecretSubStringOffset;
			auto key = pair.second.GetSecret().substr(SecretSubstringStart, offset);
			return HashCompare(pair.first, key);
		}

		/* This is not a SHA256 secured message. Authorize manually. */
		return pair.second.GetSecret() == sensor.GetSecret();
	}
}