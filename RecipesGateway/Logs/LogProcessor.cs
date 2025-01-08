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
            Log.Information("Log received from RabbitMQ: {@Message}", message);

            Log.Logger.Information("Processed log: {@Message}", message);
        });
    }
}


