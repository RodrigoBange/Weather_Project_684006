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
            logger.LogInformation("Processing message: {MessageText}", message.MessageText);

            try
            {
                // Deserialize the incoming message to extract jobId and weather data
                var payload = JsonConvert.DeserializeObject<WeatherJobPayload>(message.MessageText);

                if (payload == null || string.IsNullOrWhiteSpace(payload.JobId) || string.IsNullOrWhiteSpace(payload.WeatherData))
                {
                    logger.LogError("Invalid payload: missing jobId or weather data.");
                    return;
                }

                // Parse the weather data into a list of WeatherStation objects
                var weatherData = ParseWeatherData(payload.WeatherData);

                // Validate the parsed data
                if (weatherData.Count == 0)
                {
                    logger.LogError("Parsed weather data is empty for jobId: {JobId}", payload.JobId);
                    return;
                }

                // Add parsed weather station data to the image processing queue
                await _imageQueueClient.CreateIfNotExistsAsync();
                foreach (var station in weatherData)
                {
                    var stationPayload = new
                    {
                        payload.JobId,
                        StationData = station
                    };

                    var base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(stationPayload)));
                    await _imageQueueClient.SendMessageAsync(base64Message);
                    logger.LogInformation("Added message to the image processing queue for jobId {JobId}: {StationName}", payload.JobId, station.StationName);
                }
            }
            catch (JsonException jsonEx)
            {
                logger.LogError(jsonEx, "JSON deserialization error while processing the message.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected error occurred while processing the message.");
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

                // Convert station measurements to a list of WeatherStation objects
                results.AddRange(stationMeasurements.Select(
                    locationData => new WeatherStation
                    {
                        Id = locationData["$id"]?.ToString(),
                        StationName = locationData["stationname"]?.ToString(),
                        FeelTemperature = locationData["feeltemperature"]?.ToString()?.Replace(",", "."),
                        GroundTemperature = locationData["groundtemperature"]?.ToString()?.Replace(",", ".")
                    }));
            }
            catch (JsonException jsonEx)
            {
                Console.WriteLine($"JSON parsing error: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing weather data: {ex.Message}");
            }

            return results;
        }
    }
}