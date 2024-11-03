using Azure.Storage.Queues;

namespace Weather_Project_684006.Factories;

public class QueueClientImages
{
    public QueueClient Client { get; }

    public QueueClientImages (QueueClient client)
    {
        Client = client;
    }
}