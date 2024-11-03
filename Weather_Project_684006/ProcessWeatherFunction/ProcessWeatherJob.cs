using System.Text;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Weather_Project_684006.Factories;
using Weather_Project_684006.Models;

namespace Weather_Project_684006.ProcessWeatherFunction
{
    public class ProcessWeatherJob(ILogger<ProcessWeatherJob> logger, QueueClientFactory queueClientFactory)
    {
        private readonly QueueClient _imageQueueClient = queueClientFactory.GetQueueClient("image-processing-jobs");
        
        [Function(nameof(ProcessWeatherJob))]
        public async Task Run([QueueTrigger("weather-jobs", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            logger.LogInformation($"Processing message: {message.MessageText}");

            try
            {
                // Deserialize the raw weather data
                var weatherDataJson = message.MessageText;
                var weatherData = ParseWeatherData(weatherDataJson);
                logger.LogInformation(weatherData.Count().ToString());

                // Check if there is any weather data
                if (weatherData.Count == 0)
                {
                    logger.LogError("Failed to parse weather data.");
                    return;
                }

                // Add weather data to the queue
                await _imageQueueClient.CreateIfNotExistsAsync();
                foreach (var base64Message in weatherData.Select(station => JsonConvert.SerializeObject(station)).Select(
                             stationMessage => Convert.ToBase64String(Encoding.UTF8.GetBytes(stationMessage))))
                {
                    await _imageQueueClient.SendMessageAsync(base64Message);
                    logger.LogInformation($"Added message to the queue: {base64Message}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"An error occurred while processing the message: {ex.Message}");
            }
        }
        
        private static List<WeatherStation> ParseWeatherData(string jsonData)
        {
            var results = new List<WeatherStation>();

            try
            {
                var jsonObject = JObject.Parse(jsonData);
                var stationMeasurements = jsonObject["actual"]?["stationmeasurements"];

                if (stationMeasurements is not { HasValues: true })
                {
                    Console.WriteLine("No measurements found in the JSON.");
                    return results;
                }

                results.AddRange(stationMeasurements.Select(
                    locationData => new WeatherStation
                    {
                        Id = locationData["$id"]?.ToString(), 
                        StationName = locationData["stationname"]?.ToString(), 
                        FeelTemperature = locationData["feeltemperature"]?.ToString()?.Replace(",", "."), 
                        GroundTemperature = locationData["groundtemperature"]?.ToString()?.Replace(",", ".")
                    }));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing weather data: {ex.Message}");
            }

            return results;
        }
    }
}