using Azure.Storage.Queues;

namespace Weather_Project_684006.Factories;

public class QueueClientImages(QueueClient client)
{
    public QueueClient Client { get; } = client;
}