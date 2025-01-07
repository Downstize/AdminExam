using EasyNetQ;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RecipesGrpc.Services
{
    public class RabbitMqPublisher
    {
        private readonly IBus _bus;
        private readonly ILogger<RabbitMqPublisher> _logger;

        public RabbitMqPublisher(ILogger<RabbitMqPublisher> logger)
        {
            _logger = logger;

            try
            {
                // Загружаем конфигурацию из JSON
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                var config = builder.Build();

                // Получаем строку подключения
                var amqp = config.GetConnectionString("AutoRabbitMQ");

                // Создаем соединение с RabbitMQ
                _bus = RabbitHutch.CreateBus(amqp);

                if (!_bus.Advanced.IsConnected)
                {
                    throw new Exception("Соединение с RabbitMQ не установлено.");
                }

                _logger.LogInformation("Подключение к RabbitMQ успешно установлено.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка подключения к RabbitMQ: {ex.Message}");
                throw;
            }
        }

        public void PublishMessage<T>(T message) where T : class
        {
            try
            {
                if (!_bus.Advanced.IsConnected)
                {
                    throw new Exception("Соединение с RabbitMQ отсутствует.");
                }

                // Публикуем сообщение
                _bus.PubSub.Publish(message);
                _logger.LogInformation("Сообщение опубликовано в RabbitMQ: {@Message}", message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка публикации сообщения в RabbitMQ: {ex.Message}");
                throw;
            }
        }

        public async Task SubscribeToMessagesAsync<TMessage>(string subscriptionId, Func<TMessage, Task> onMessage) where TMessage : class
        {
            try
            {
                if (!_bus.Advanced.IsConnected)
                {
                    throw new Exception("Соединение с RabbitMQ отсутствует.");
                }

                _logger.LogInformation("Подписываемся на сообщения типа {MessageType} с ID {SubscriptionId}", typeof(TMessage).Name, subscriptionId);

                // Подписываемся на сообщения
                await _bus.PubSub.SubscribeAsync(subscriptionId, onMessage);

                _logger.LogInformation("Подписка на сообщения типа {MessageType} с ID {SubscriptionId} выполнена.", typeof(TMessage).Name, subscriptionId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка подписки на сообщения: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _bus?.Dispose();
            _logger.LogInformation("Соединение с RabbitMQ закрыто.");
        }
    }
}
