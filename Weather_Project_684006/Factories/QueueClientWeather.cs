using Azure.Storage.Queues;

namespace Weather_Project_684006.Factories;

public class QueueClientWeather
{
    public QueueClient Client { get; }

    public QueueClientWeather(QueueClient client)
    {
        Client = client;
    }
}