using RabbitMQ.Client;
using System;
using System.Text;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NotesHubApi
{
    public interface IRabbitMQService
    {
        void PublishMessage(string queueName, object message);
    }

    public class RabbitMQService : IRabbitMQService, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly ILogger<RabbitMQService> _logger;

        public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
        {
            _logger = logger;

            try
            {
                var factory = new ConnectionFactory
                {
                    HostName = configuration["RabbitMQ:HostName"],
                    UserName = configuration["RabbitMQ:UserName"],
                    Password = configuration["RabbitMQ:Password"]
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();
                _logger.LogInformation("RabbitMQ connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize RabbitMQ connection");
                throw;
            }
        }

        public void PublishMessage(string queueName, object message)
        {
            try
            {
                _channel.QueueDeclare(queue: queueName,
                                      durable: false,
                                      exclusive: false,
                                      autoDelete: false,
                                      arguments: null);

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

                _channel.BasicPublish(exchange: "",
                                      routingKey: queueName,
                                      basicProperties: null,
                                      body: body);

                _logger.LogInformation("Message published to queue: {QueueName}. Message: {@Message}", queueName, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to queue: {QueueName}. Message: {@Message}", queueName, message);
                throw;
            }
        }

        public void Dispose()
        {
            try
            {
                _channel?.Close();
                _channel?.Dispose();
                _connection?.Close();
                _connection?.Dispose();
                _logger.LogInformation("RabbitMQ connection closed and disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during RabbitMQ service disposal");
            }
        }
    }
}
