using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;

namespace Weather_Project_684006.Factories
{
    public class QueueClientFactory(IServiceProvider serviceProvider)
    {
        public QueueClient GetQueueClient(string queueName)
        {
            return queueName switch
            {
                "weather-jobs" => serviceProvider.GetRequiredService<QueueClientWeather>().Client,
                "image-processing-jobs" => serviceProvider.GetRequiredService<QueueClientImages>().Client,
                _ => throw new ArgumentException("Invalid queue name")
            };
        }
    }
}