namespace RecipesGateway.Logs;

using EasyNetQ;
using Serilog;

public class LogProcessor
{
    private readonly IBus _rabbitMqBus;

    public LogProcessor(IBus rabbitMqBus)
    {
        _rabbitMqBus = rabbitMqBus;
    }

    public void StartListening()
    {
        _rabbitMqBus.PubSub.SubscribeAsync<dynamic>("LogSubscriber", async message =>
        {
            // Логируем сообщение из RabbitMQ через Serilog
            Log.Information("Log received from RabbitMQ: {@Message}", message);

            // Отправляем лог в Logstash через Serilog
            Log.Logger.Information("Processed log: {@Message}", message);
        });
    }
}


