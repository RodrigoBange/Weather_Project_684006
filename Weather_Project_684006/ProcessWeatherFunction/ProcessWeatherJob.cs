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
    public class ProcessWeatherJob
    {
        private readonly ILogger<ProcessWeatherJob> _logger;
        private readonly QueueClient _imageQueueClient;

        public ProcessWeatherJob(ILogger<ProcessWeatherJob> logger, QueueClientFactory queueClientFactory)
        {
            _logger = logger;
            _imageQueueClient = queueClientFactory.GetQueueClient("image-processing-jobs");
        }

        [Function(nameof(ProcessWeatherJob))]
        public async Task Run([QueueTrigger("weather-jobs", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation($"Processing message: {message.MessageText}");

            try
            {
                // Deserialize the message and extract jobId and weather data
                var payload = JsonConvert.DeserializeObject<dynamic>(message.MessageText);
                string jobId = payload?.JobId;
                string weatherDataJson = payload?.WeatherData;

                var weatherData = ParseWeatherData(weatherDataJson);

                // Check if there is any weather data
                if (weatherData.Count == 0 || string.IsNullOrEmpty(jobId))
                {
                    _logger.LogError("Failed to parse weather data or missing jobId.");
                    return;
                }

                // Add weather data to the image processing queue with the jobId
                await _imageQueueClient.CreateIfNotExistsAsync();
                foreach (var station in weatherData)
                {
                    var stationPayload = new
                    {
                        JobId = jobId,
                        StationData = station
                    };
                    var base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(stationPayload)));
                    await _imageQueueClient.SendMessageAsync(base64Message);
                    _logger.LogInformation($"Added message to the image processing queue for jobId {jobId}: {base64Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while processing the message: {ex.Message}");
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