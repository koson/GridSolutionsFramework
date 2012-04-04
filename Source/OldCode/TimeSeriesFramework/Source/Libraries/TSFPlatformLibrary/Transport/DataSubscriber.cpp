//******************************************************************************************************
//  DataSubscriber.cpp - Gbtc
//
//  Copyright � 2010, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the Eclipse Public License -v 1.0 (the "License"); you may
//  not use this file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://www.opensource.org/licenses/eclipse-1.0.php
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  03/26/2012 - Stephen C. Wills
//       Generated original version of source code.
//
//******************************************************************************************************

#include <sstream>
#include <exception>
#include <boost/bind.hpp>

#include "Constants.h"
#include "DataSubscriber.h"
#include "CompactMeasurementParser.h"

namespace tsf = TimeSeriesFramework;

// Convenience functions to perform simple conversions.
template <class T>
std::string ToString(const T& obj);
tsf::Guid ToGuid(uint8_t* data);

// Deconstructor calls disconnect to clean up after itself.
tsf::Transport::DataSubscriber::~DataSubscriber()
{
	Disconnect();
}

// All commands to server are sent on the command thread from here.
void tsf::Transport::DataSubscriber::RunCommandThread()
{
	CommandPacket packet;

	while(true)
	{
		m_commandQueue.WaitForData();

		if (m_disconnecting)
			break;

		packet = m_commandQueue.Dequeue();
		boost::asio::write(m_commandChannelSocket, boost::asio::buffer(packet));
	}
}

// All callbacks are run from the callback thread from here.
void tsf::Transport::DataSubscriber::RunCallbackThread()
{
	CallbackDispatcher dispatcher;

	while (true)
	{
		m_callbackQueue.WaitForData();

		if (m_disconnecting)
			break;

		dispatcher = m_callbackQueue.Dequeue();
		dispatcher.Function(dispatcher.Source, dispatcher.Data);
	}
}

// All responses received from the server are handled by this thread with the
// exception of data packets which may or may not be handled by this thread.
void tsf::Transport::DataSubscriber::RunCommandChannelResponseThread()
{
	const std::size_t PayloadHeaderSize = 8;
	const std::size_t PacketSizeOffset = 4;

	std::vector<uint8_t> buffer(65536);
	boost::system::error_code error;
	std::stringstream errorMessageStream;

	int32_t* packetSizePtr;
	int32_t packetSize;

	while (true)
	{
		// Read payload header
		// This read method is guaranteed not to return until the
		// requested size has been read or an error has occurred.
		boost::asio::read(m_commandChannelSocket, boost::asio::buffer(buffer, PayloadHeaderSize), error);

		if (m_disconnecting)
			break;

		if (error == boost::asio::error::eof)
		{
			// Connection closed by peer
			boost::thread t(boost::bind(&tsf::Transport::DataSubscriber::ConnectionTerminatedDispatcher, this));
			break;
		}
		
		if (error)
		{
			errorMessageStream << "Error reading data from command channel: ";
			errorMessageStream << boost::system::system_error(error).what();
			DispatchErrorMessage(errorMessageStream.str());
			break;
		}
		
		// Gather statistics
		m_totalCommandChannelBytesReceived += PayloadHeaderSize;

		// Parse payload header
		packetSizePtr = (int32_t*)&buffer[PacketSizeOffset];
		packetSize = m_endianConverter.ConvertLittleEndian(*packetSizePtr);
		buffer.reserve(packetSize);

		// Read packet (payload body)
		// This read method is guaranteed not to return until the
		// requested size has been read or an error has occurred.
		boost::asio::read(m_commandChannelSocket, boost::asio::buffer(buffer, packetSize), error);

		if (m_disconnecting)
			break;

		if (error == boost::asio::error::eof)
		{
			// Connection closed by peer
			boost::thread t(boost::bind(&tsf::Transport::DataSubscriber::ConnectionTerminatedDispatcher, this));
			break;
		}

		if (error)
		{
			errorMessageStream << "Error reading data from command channel: ";
			errorMessageStream << boost::system::system_error(error).what();
			DispatchErrorMessage(errorMessageStream.str());
			break;
		}

		// Gather statistics
		m_totalCommandChannelBytesReceived += packetSize;

		// Process response
		ProcessServerResponse(&buffer[0], 0, packetSize);
	}
}

// If the user defines a separate UDP channel for their
// subscription, data packets get handled from this thread.
void tsf::Transport::DataSubscriber::RunDataChannelResponseThread()
{
	std::vector<uint8_t> buffer(MaxPacketSize);
	std::size_t length;

	boost::asio::ip::udp::endpoint endpoint(m_hostAddress, 0);
	boost::system::error_code error;
	std::stringstream errorMessageStream;

	while (true)
	{
		length = m_dataChannelSocket.receive_from(boost::asio::buffer(buffer), endpoint, 0, error);

		if (m_disconnecting)
			break;

		if (error)
		{
			errorMessageStream << "Error reading data from command channel: ";
			errorMessageStream << boost::system::system_error(error).what();
			DispatchErrorMessage(errorMessageStream.str());
			break;
		}

		ProcessServerResponse(&buffer[0], 0, length);
	}
}

// Handles success messages received from the server.
void tsf::Transport::DataSubscriber::HandleSucceeded(uint8_t commandCode, uint8_t* data, std::size_t offset, std::size_t length)
{
	const std::size_t CharSize = sizeof(char);

	std::size_t messageLength = length / CharSize;
	std::stringstream messageStream;

	char* messageStart;
	char* messageEnd;
	char* messageIter;
	
	switch (commandCode)
	{
	case ServerCommand::MetadataRefresh:
		// Metadata refresh message is not sent with a
		// message, but rather the metadata itself.
		HandleMetadataRefresh(data, offset, length);
		break;
		
	case ServerCommand::Subscribe:
	case ServerCommand::Unsubscribe:
		// Do not break on these messages because there is
		// still an associated message to be processed.
		m_subscribed = (commandCode == ServerCommand::Subscribe);

	case ServerCommand::Authenticate:
	case ServerCommand::RotateCipherKeys:
		// Each of these responses come with a message that will
		// be delivered to the user via the status message callback.
		messageStart = (char*)(data + offset);
		messageEnd = messageStart + messageLength;
		messageStream << "Received success code in response to server command 0x" << std::hex << (int)commandCode << ": ";

		for (messageIter = messageStart; messageIter < messageEnd; ++messageIter)
			messageStream << *messageIter;

		DispatchStatusMessage(messageStream.str());
		break;

	default:
		// If we don't know what the message is, we can't interpret
		// the data sent with the packet. Deliver an error message
		// to the user via the error message callback.
		messageStream << "Received success code in response to unknown server command 0x" << std::hex << (int)commandCode;
		DispatchErrorMessage(messageStream.str());
		break;
	}
}

// Handles failure messages from the server.
void tsf::Transport::DataSubscriber::HandleFailed(uint8_t commandCode, uint8_t* data, std::size_t offset, std::size_t length)
{
	const std::size_t CharSize = sizeof(char);

	std::size_t messageLength = length / CharSize;
	std::stringstream messageStream;

	char* messageStart;
	char* messageEnd;
	char* messageIter;

	messageStart = (char*)(data + offset);
	messageEnd = messageStart + messageLength;
	messageStream << "Received failure code from server command 0x" << std::hex << (int)commandCode << ": ";

	for (messageIter = messageStart; messageIter < messageEnd; ++messageIter)
		messageStream << *messageIter;

	DispatchErrorMessage(messageStream.str());
}

// Handles metadata refresh messages from the server.
void tsf::Transport::DataSubscriber::HandleMetadataRefresh(uint8_t* data, std::size_t offset, std::size_t length)
{
	Dispatch(&MetadataDispatcher, data, offset, length);
}

// Handles data packets from the server.
void tsf::Transport::DataSubscriber::HandleDataPacket(uint8_t* data, std::size_t offset, std::size_t length)
{
	Dispatch(&NewMeasurementsDispatcher, data, offset, length);
}

// Handles data start time reported by the server at the beginning of a subscription.
void tsf::Transport::DataSubscriber::HandleDataStartTime(uint8_t* data, std::size_t offset, std::size_t length)
{
	Dispatch(&DataStartTimeDispatcher, data, offset, length);
}

// Handles processing complete message sent by the server at the end of a temporal session.
void tsf::Transport::DataSubscriber::HandleProcessingComplete(uint8_t* data, std::size_t offset, std::size_t length)
{
	Dispatch(&ProcessingCompleteDispatcher, data, offset, length);
}

// Cache signal IDs sent by the server into the signal index cache.
void tsf::Transport::DataSubscriber::HandleUpdateSignalIndexCache(uint8_t* data, std::size_t offset, std::size_t length)
{
	const std::size_t CharSize = sizeof(char);

	int32_t* referenceCountPtr;
	int32_t referenceCount;

	uint16_t* signalIndexPtr;
	uint8_t* signalIDPtr;
	int32_t* sourceSizePtr;
	char* sourcePtr;
	uint32_t* idPtr;

	uint16_t signalIndex;
	Guid signalID;
	std::size_t sourceSize;
	std::string source;
	uint32_t id;

	std::stringstream sourceStream;
	char* sourceIter;
	int i;

	// Begin by emptying the cache
	m_signalIndexCache.Clear();

	// Skip 4-byte length and 16-byte subscriber ID
	// We may need to parse these in the future...
	referenceCountPtr = (int32_t*)(data + offset + 20);
	referenceCount = m_endianConverter.ConvertBigEndian(*referenceCountPtr);

	// Set up signalIndexPtr before entering the loop
	signalIndexPtr = (uint16_t*)(referenceCountPtr + 1);

	for (i = 0; i < referenceCount; ++i)
	{
		// Begin setting up pointers
		signalIDPtr = (uint8_t*)(signalIndexPtr + 1);
		sourceSizePtr = (int32_t*)(signalIDPtr + 16);

		// Get the source size now so we can use it to find the ID
		sourceSize = (std::size_t)m_endianConverter.ConvertBigEndian(*sourceSizePtr) / CharSize;

		// Continue setting up pointers
		sourcePtr = (char*)(sourceSizePtr + 1);
		idPtr = (uint32_t*)(sourcePtr + sourceSize);

		// Build string from binary data
		for (sourceIter = sourcePtr; sourceIter < sourcePtr + sourceSize; ++sourceIter)
			sourceStream << *sourceIter;

		// Set values for measurement key
		signalIndex = m_endianConverter.ConvertBigEndian(*signalIndexPtr);
		signalID = ToGuid(signalIDPtr);
		source = sourceStream.str();
		id = m_endianConverter.ConvertBigEndian(*idPtr);

		// Add measurement key to the cache
		m_signalIndexCache.AddMeasurementKey(signalIndex, signalID, source, id);

		// Advance signalIndexPtr to the next signal
		// index and clear out the string stream
		signalIndexPtr = (uint16_t*)(idPtr + 1);
		sourceStream.str("");
	}

	// There is additional data about unauthorized signal
	// IDs that may need to be parsed in the future...
}

// Updates base time offsets.
void tsf::Transport::DataSubscriber::HandleUpdateBaseTimes(uint8_t* data, std::size_t offset, std::size_t length)
{
	int32_t* timeIndexPtr = (int32_t*)(data + offset);
	int64_t* timeOffsetsPtr = (int64_t*)(timeIndexPtr + 1);

	m_timeIndex = (std::size_t)m_endianConverter.ConvertBigEndian(*timeIndexPtr);
	m_baseTimeOffsets[0] = m_endianConverter.ConvertBigEndian(timeOffsetsPtr[0]);
	m_baseTimeOffsets[1] = m_endianConverter.ConvertBigEndian(timeOffsetsPtr[1]);
}

// Dispatches the given function to the callback thread.
void tsf::Transport::DataSubscriber::Dispatch(DispatcherFunction function)
{
	Dispatch(function, 0, 0, 0);
}

// Dispatches the given function to the callback thread and provides the given data to that function when it is called.
void tsf::Transport::DataSubscriber::Dispatch(DispatcherFunction function, uint8_t* data, std::size_t offset, std::size_t length)
{
	CallbackDispatcher dispatcher;
	std::vector<uint8_t> dataVector(length);
	int i;

	if (data != 0)
	{
		for (i = 0; i < length; ++i)
			dataVector[i] = data[offset + i];
	}

	dispatcher.Source = this;
	dispatcher.Data = dataVector;
	dispatcher.Function = function;

	m_callbackQueue.Enqueue(dispatcher);
}

// Invokes the status message callback on the callback thread and provides the given message to it.
void tsf::Transport::DataSubscriber::DispatchStatusMessage(std::string message)
{
	const std::size_t CharSize = sizeof(char);
	std::size_t messageSize = message.size() * CharSize;
	Dispatch(&StatusMessageDispatcher, (uint8_t*)message.data(), 0, messageSize);
}

// Invokes the error message callback on the callback thread and provides the given message to it.
void tsf::Transport::DataSubscriber::DispatchErrorMessage(std::string message)
{
	const std::size_t CharSize = sizeof(char);
	std::size_t messageSize = message.size() * CharSize;
	Dispatch(&ErrorMessageDispatcher, (uint8_t*)message.data(), 0, messageSize);
}

// Dispatcher function for status messages. Decodes the message and provides it to the user via the status message callback.
void tsf::Transport::DataSubscriber::StatusMessageDispatcher(DataSubscriber* source, std::vector<uint8_t> data)
{
	MessageCallback statusMessageCallback = source->m_statusMessageCallback;
	std::stringstream messageStream;
	std::size_t i;

	for (i = 0; i < data.size(); ++i)
		messageStream << data[i];

	if (statusMessageCallback != 0)
		statusMessageCallback(messageStream.str());
}

// Dispatcher function for error messages. Decodes the message and provides it to the user via the error message callback.
void tsf::Transport::DataSubscriber::ErrorMessageDispatcher(DataSubscriber* source, std::vector<uint8_t> data)
{
	MessageCallback errorMessageCallback = source->m_errorMessageCallback;
	std::stringstream messageStream;
	std::size_t i;

	for (i = 0; i < data.size(); ++i)
		messageStream << data[i];

	if (errorMessageCallback != 0)
		errorMessageCallback(messageStream.str());
}

// Dispatcher function for data start time. Decodes the start time and provides it to the user via the data start time callback.
void tsf::Transport::DataSubscriber::DataStartTimeDispatcher(DataSubscriber* source, std::vector<uint8_t> data)
{
	DataStartTimeCallback dataStartTimeCallback = source->m_dataStartTimeCallback;
	EndianConverter endianConverter = source->m_endianConverter;
	int64_t* dataPtr = (int64_t*)&data[0];
	int64_t dataStartTime = endianConverter.ConvertBigEndian(*dataPtr);

	if (dataStartTimeCallback != 0)
		dataStartTimeCallback(dataStartTime);
}

// Dispatcher function for metadata. Provides encoded metadata to the user via the metadata callback.
void tsf::Transport::DataSubscriber::MetadataDispatcher(DataSubscriber* source, std::vector<uint8_t> data)
{
	MetadataCallback metadataCallback = source->m_metadataCallback;

	if (metadataCallback != 0)
		metadataCallback(data);
}

// Dispatcher function for new measurements. Decodes the measurements and provides them to the user via the new measurements callback.
void tsf::Transport::DataSubscriber::NewMeasurementsDispatcher(DataSubscriber* source, std::vector<uint8_t> data)
{
	NewMeasurementsCallback newMeasurementsCallback = source->m_newMeasurementsCallback;
	MessageCallback errorMessageCallback = source->m_errorMessageCallback;
	SubscriptionInfo& info = source->m_currentSubscription;

	CompactMeasurementParser measurementParser(source->m_signalIndexCache, source->m_baseTimeOffsets, info.IncludeTime, info.UseMillisecondResolution);
	Measurement parsedMeasurement;
	std::vector<Measurement> newMeasurements;

	uint8_t dataPacketFlags;
	int32_t* measurementCountPtr;
	int64_t* frameLevelTimestampPtr = 0;
	int64_t frameLevelTimestamp;

	uint8_t* buffer;
	std::size_t offset = 0;
	std::size_t length = 0;

	// Read data packet flags
	dataPacketFlags = data[0];
	++offset;

	// Read measurement count and gather statistics
	measurementCountPtr = (int32_t*)&data[1];
	source->m_totalMeasurementsReceived += source->m_endianConverter.ConvertBigEndian(*measurementCountPtr);
	offset += 4;

	// Read frame-level timestamp, if available
	if (dataPacketFlags & DataPacketFlags::Synchronized)
	{
		frameLevelTimestampPtr = (int64_t*)(measurementCountPtr + 1);
		frameLevelTimestamp = source->m_endianConverter.ConvertBigEndian(*frameLevelTimestampPtr);
		offset += 8;
	}

	// Set up buffer and length for measurement parsing
	buffer = &data[0];
	length = data.size() - offset;

	if (newMeasurementsCallback != 0)
	{
		while (length > 0)
		{
			if (!measurementParser.TryParseMeasurement(buffer, offset, length))
			{
				errorMessageCallback("Error parsing measurement");
				break;
			}

			parsedMeasurement = measurementParser.GetParsedMeasurement();

			if (frameLevelTimestampPtr != 0)
				parsedMeasurement.Timestamp = frameLevelTimestamp;

			newMeasurements.push_back(measurementParser.GetParsedMeasurement());
		}

		newMeasurementsCallback(newMeasurements);
	}
}

// Dispatcher for processing complete message that is sent by the server at the end of a temporal session.
void tsf::Transport::DataSubscriber::ProcessingCompleteDispatcher(DataSubscriber* source, std::vector<uint8_t> data)
{
	const std::size_t CharSize = sizeof(char);

	MessageCallback processingCompleteCallback;

	std::size_t messageLength;
	std::stringstream messageStream;

	char* messageStart;
	char* messageEnd;
	char* messageIter;

	processingCompleteCallback = source->m_processingCompleteCallback;

	if (processingCompleteCallback != 0)
	{
		messageLength = data.size() / CharSize;
		messageStart = (char*)&data[0];
		messageEnd = messageStart + messageLength;

		for (messageIter = messageStart; messageIter < messageEnd; ++messageIter)
			messageStream << *messageIter;

		processingCompleteCallback(messageStream.str());
	}
}

// Dispatcher for connection terminated. This is called from its own separate thread
// in order to cleanly shut down the subscriber in case the connection was terminated
// by the peer. Additionally, this allows the user to automatically reconnect in their
// callback function without having to spawn their own separate thread.
void tsf::Transport::DataSubscriber::ConnectionTerminatedDispatcher()
{
	Disconnect();

	if (m_connectionTerminatedCallback != 0)
		m_connectionTerminatedCallback();
}

// Processes a response sent by the server. Response codes are defined in the header file "Constants.h".
void tsf::Transport::DataSubscriber::ProcessServerResponse(uint8_t* buffer, std::size_t offset, std::size_t length)
{
	const std::size_t PacketHeaderSize = 6;

	uint8_t* packetBodyStart = buffer + PacketHeaderSize;
	std::size_t packetBodyLength = length - PacketHeaderSize;
	
	uint8_t responseCode = buffer[0];
	uint8_t commandCode = buffer[1];

	switch (responseCode)
	{
	case ServerResponse::Succeeded:
		HandleSucceeded(commandCode, packetBodyStart, 0, packetBodyLength);
		break;

	case ServerResponse::Failed:
		HandleFailed(commandCode, packetBodyStart, 0, packetBodyLength);
		break;

	case ServerResponse::DataPacket:
		HandleDataPacket(packetBodyStart, 0, packetBodyLength);
		break;

	case ServerResponse::DataStartTime:
		HandleDataStartTime(packetBodyStart, 0, packetBodyLength);
		break;

	case ServerResponse::ProcessingComplete:
		HandleProcessingComplete(packetBodyStart, 0, packetBodyLength);
		break;

	case ServerResponse::UpdateSignalIndexCache:
		HandleUpdateSignalIndexCache(packetBodyStart, 0, packetBodyLength);
		break;

	case ServerResponse::UpdateBaseTimes:
		HandleUpdateBaseTimes(packetBodyStart, 0, packetBodyLength);
		break;
	}
}

// Registers the status message callback.
void tsf::Transport::DataSubscriber::RegisterStatusMessageCallback(MessageCallback statusMessageCallback)
{
	m_statusMessageCallback = statusMessageCallback;
}

// Registers the error message callback.
void tsf::Transport::DataSubscriber::RegisterErrorMessageCallback(MessageCallback errorMessageCallback)
{
	m_errorMessageCallback = errorMessageCallback;
}

// Registers the data start time callback.
void tsf::Transport::DataSubscriber::RegisterDataStartTimeCallback(DataStartTimeCallback dataStartTimeCallback)
{
	m_dataStartTimeCallback = dataStartTimeCallback;
}

// Registers the metadata callback.
void tsf::Transport::DataSubscriber::RegisterMetadataCallback(MetadataCallback metadataCallback)
{
	m_metadataCallback = metadataCallback;
}

// Registers the new measurements callback.
void tsf::Transport::DataSubscriber::RegisterNewMeasurementsCallback(NewMeasurementsCallback newMeasurementsCallback)
{
	m_newMeasurementsCallback = newMeasurementsCallback;
}

// Registers the processing complete callback.
void tsf::Transport::DataSubscriber::RegisterProcessingCompleteCallback(MessageCallback processingCompleteCallback)
{
	m_processingCompleteCallback = processingCompleteCallback;
}

// Registers the connection terminated callback.
void tsf::Transport::DataSubscriber::RegisterConnectionTerminatedCallback(ConnectionTerminatedCallback connectionTerminatedCallback)
{
	m_connectionTerminatedCallback = connectionTerminatedCallback;
}

// Returns true if metadata exchange is compressed.
bool tsf::Transport::DataSubscriber::IsMetadataCompressed() const
{
	return m_compressMetadata;
}

// Set the value which determines whether metadata exchange is compressed.
void tsf::Transport::DataSubscriber::SetMetadataCompressed(bool compressed)
{
	m_compressMetadata = compressed;

	if (m_commandChannelSocket.is_open())
		SendOperationalModes();
}

// Synchronously connects to publisher.
void tsf::Transport::DataSubscriber::Connect(std::string hostname, uint16_t port)
{
	boost::asio::ip::tcp::resolver resolver(m_commandChannelService);
	boost::asio::ip::tcp::resolver::query query(hostname, ToString(port));
	boost::asio::ip::tcp::resolver::iterator endpointIterator = resolver.resolve(query);
	boost::asio::ip::tcp::resolver::iterator hostEndpoint;
	boost::system::error_code error;

	m_totalCommandChannelBytesReceived = 0L;
	m_totalDataChannelBytesReceived = 0L;
	m_totalMeasurementsReceived = 0L;

	if (m_commandChannelSocket.is_open())
		throw SubscriberException("Subscriber is already connected; disconnect first");

	hostEndpoint = boost::asio::connect(m_commandChannelSocket, endpointIterator, error);

	if (error)
		throw boost::system::system_error(error);

	if (!m_commandChannelSocket.is_open())
		throw SubscriberException("Failed to connect to host");

	m_hostAddress = hostEndpoint->endpoint().address();

	m_commandThread = boost::thread(boost::bind(&tsf::Transport::DataSubscriber::RunCommandThread, this));
	m_callbackThread = boost::thread(boost::bind(&tsf::Transport::DataSubscriber::RunCallbackThread, this));
	m_commandChannelResponseThread = boost::thread(boost::bind(&tsf::Transport::DataSubscriber::RunCommandChannelResponseThread, this));

	SendOperationalModes();
	m_connected = true;
}

// Disconnects from the publisher.
void tsf::Transport::DataSubscriber::Disconnect()
{
	boost::system::error_code error;

	// Notify running threads that
	// the subscriber is disconnecting
	m_disconnecting = true;

	// Release queues and close sockets so
	// that threads can shut down gracefully
	m_commandQueue.Release();
	m_callbackQueue.Release();
	m_commandChannelSocket.close(error);
	m_dataChannelSocket.shutdown(boost::asio::ip::udp::socket::shutdown_receive, error);
	m_dataChannelSocket.close(error);

	// Join with all threads to guarantee their completion
	// before returning control to the caller
	m_commandThread.join();
	m_callbackThread.join();
	m_commandChannelResponseThread.join();
	m_dataChannelResponseThread.join();

	// Empty queues and reset them so they can be used
	// again later if the user decides to reconnect
	m_commandQueue.Clear();
	m_commandQueue.Reset();
	m_callbackQueue.Clear();
	m_callbackQueue.Reset();

	// Disconnect completed
	m_subscribed = false;
	m_disconnecting = false;
	m_connected = false;
}

// Subscribe to publisher in order to start receving data.
void tsf::Transport::DataSubscriber::Subscribe(tsf::Transport::SubscriptionInfo info)
{
	const std::size_t CharSize = sizeof(char);

	boost::asio::ip::udp ipVersion = boost::asio::ip::udp::v4();

	std::stringstream stringStream;
	std::string connectionString;

	std::vector<uint8_t> buffer;
	uint8_t* connectionStringPtr;
	uint32_t connectionStringSize;
	uint32_t bigEndianConnectionStringSize;
	uint8_t* bigEndianConnectionStringSizePtr;

	std::size_t bufferSize;
	std::size_t i;

	// Make sure to unsubscribe before attempting another
	// subscription so we don't leave connections open
	if (m_subscribed)
		Unsubscribe();

	m_currentSubscription = info;
	m_totalMeasurementsReceived = 0L;

	stringStream << "trackLatestMeasurements=" << info.Throttled << ";";
	stringStream << "includeTime=" << info.IncludeTime << ";";
	stringStream << "lagTime=" << info.LagTime << ";";
	stringStream << "leadTime=" << info.LeadTime << ";";
	stringStream << "useLocalClockAsRealTime=" << info.UseLocalClockAsRealTime << ";";
	stringStream << "processingInterval=" << info.ProcessingInterval << ";";
	stringStream << "useMillisecondResolution=" << info.UseMillisecondResolution << ";";
	//stringStream << "assemblyInfo={source=???;version=???;buildDate=???};";
	
	if (!info.FilterExpression.empty())
		stringStream << "inputMeasurementKeys={" << info.FilterExpression << "};";

	if (info.UdpDataChannel)
	{
		// Attempt to bind to local UDP port
		if (m_hostAddress.is_v6())
			ipVersion = boost::asio::ip::udp::v6();

		m_dataChannelSocket.open(ipVersion);
		m_dataChannelSocket.bind(boost::asio::ip::udp::endpoint(ipVersion, info.DataChannelLocalPort));
		m_dataChannelResponseThread = boost::thread(boost::bind(&tsf::Transport::DataSubscriber::RunDataChannelResponseThread, this));

		if (!m_dataChannelSocket.is_open())
			throw SubscriberException("Failed to bind to local port");

		stringStream << "dataChannel={";

		if (!info.DataChannelInterface.empty())
			stringStream << "interface=" << info.DataChannelInterface << ";";

		stringStream << "localport=" << info.DataChannelLocalPort << "};";
	}

	if (!info.StartTime.empty())
		stringStream << "startTimeConstraint=" << info.StartTime << ";";

	if (!info.StopTime.empty())
		stringStream << "stopTimeConstraint=" << info.StopTime << ";";

	if (!info.ConstraintParameters.empty())
		stringStream << "timeConstraintParameters=" << info.ConstraintParameters << ";";

	if (!info.WaitHandleNames.empty())
	{
		stringStream << "waitHandleNames=" << info.WaitHandleNames << ";";
		stringStream << "waitHandleTimeout=" << info.WaitHandleTimeout << ";";
	}

	if (!info.ExtraConnectionStringParameters.empty())
		stringStream << info.ExtraConnectionStringParameters << ";";

	connectionString = stringStream.str();
	connectionStringPtr = (uint8_t*)&connectionString[0];
	connectionStringSize = (uint32_t)(connectionString.size() * CharSize);
	bigEndianConnectionStringSize = m_endianConverter.ConvertBigEndian(connectionStringSize);
	bigEndianConnectionStringSizePtr = (uint8_t*)&bigEndianConnectionStringSize;

	bufferSize = 5 + connectionStringSize;
	buffer.reserve(bufferSize);

	buffer[0] = 0x02;
	buffer[1] = bigEndianConnectionStringSizePtr[0];
	buffer[2] = bigEndianConnectionStringSizePtr[1];
	buffer[3] = bigEndianConnectionStringSizePtr[2];
	buffer[4] = bigEndianConnectionStringSizePtr[3];

	for (i = 0; i < connectionStringSize; ++i)
		buffer[5 + i] = connectionStringPtr[i];

	SendServerCommand(ServerCommand::Subscribe, &buffer[0], 0, bufferSize);
}

// Returns the subscription info object used to define the most recent subscription.
tsf::Transport::SubscriptionInfo tsf::Transport::DataSubscriber::GetCurrentSubscription() const
{
	return m_currentSubscription;
}

// Unsubscribe from publisher to stop receiving data.
void tsf::Transport::DataSubscriber::Unsubscribe()
{
	boost::system::error_code error;

	m_disconnecting = true;
	m_dataChannelSocket.shutdown(boost::asio::ip::udp::socket::shutdown_receive, error);
	m_dataChannelSocket.close(error);
	m_dataChannelResponseThread.join();
	m_disconnecting = false;

	SendServerCommand(ServerCommand::Unsubscribe);
}

// Sends a command to the server.
void tsf::Transport::DataSubscriber::SendServerCommand(uint8_t commandCode)
{
	SendServerCommand(commandCode, 0, 0, 0);
}

// Sends a command along with the given data to the server.
void tsf::Transport::DataSubscriber::SendServerCommand(uint8_t commandCode, uint8_t* data, std::size_t offset, std::size_t length)
{
	std::size_t packetSize = 1 + length;
	int32_t littleEndianPacketSize = m_endianConverter.ConvertLittleEndian((int32_t)packetSize);
	uint8_t* littleEndianPacketSizePtr = (uint8_t*)&littleEndianPacketSize;

	CommandPacket packet(8 + packetSize);
	std::size_t i;
	
	// Insert payload marker
	packet[0] = 0xAA;
	packet[1] = 0xBB;
	packet[2] = 0xCC;
	packet[3] = 0xDD;

	// Insert packet size
	packet[4] = littleEndianPacketSizePtr[0];
	packet[5] = littleEndianPacketSizePtr[1];
	packet[6] = littleEndianPacketSizePtr[2];
	packet[7] = littleEndianPacketSizePtr[3];

	// Insert command code
	packet[8] = commandCode;

	if (data != 0)
	{
		for (i = 0; i < length; ++i)
			packet[9 + i] = data[offset + i];
	}

	m_commandQueue.Enqueue(packet);
}

// Convenience method to send the currently defined
// and/or supported operational modes to the server.
void tsf::Transport::DataSubscriber::SendOperationalModes()
{
	uint32_t operationalModes = OperationalModes::NoFlags;
	uint32_t bigEndianOperationalModes;

	operationalModes |= OperationalEncoding::UTF8;
	operationalModes |= OperationalModes::UseCommonSerializationFormat;

	if (m_compressMetadata)
		operationalModes |= OperationalModes::CompressMetadata;

	bigEndianOperationalModes = m_endianConverter.ConvertBigEndian(operationalModes);
	SendServerCommand(ServerCommand::DefineOperationalModes, (uint8_t*)&bigEndianOperationalModes, 0, 4);
}

// Gets the total number of bytes received via the command channel since last connection.
long tsf::Transport::DataSubscriber::GetTotalCommandChannelBytesReceived() const
{
	return m_totalCommandChannelBytesReceived;
}

// Gets the total number of bytes received via the data channel since last connection.
long tsf::Transport::DataSubscriber::GetTotalDataChannelBytesReceived() const
{
	return m_totalDataChannelBytesReceived;
}

// Gets the total number of measurements received since last subscription.
long tsf::Transport::DataSubscriber::GetTotalMeasurementsReceived() const
{
	return m_totalMeasurementsReceived;
}

// Indicates whether the subscriber is connected.
bool tsf::Transport::DataSubscriber::IsConnected() const
{
	return m_connected;
}

// Indicates whether the subscriber is subscribed.
bool tsf::Transport::DataSubscriber::IsSubscribed() const
{
	return m_subscribed;
}

// Converts an object to a string.
template <class T>
std::string ToString(const T& obj)
{
	std::stringstream stringStream;
	stringStream << obj;
	return stringStream.str();
}

// Converts 16 contiguous bytes of data into a globally unique identifier.
tsf::Guid ToGuid(uint8_t* data)
{
	tsf::Guid id;
	tsf::Guid::iterator iter;

	for (iter = id.begin(); iter != id.end(); ++iter, ++data)
		*iter = (tsf::Guid::value_type)*data;

	return id;
}