using System.Text;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Weather_Project_684006.Factories;

namespace Weather_Project_684006.StartWeatherJobFunction
{
    public class StartWeatherJob(ILogger<StartWeatherJob> logger, QueueClientFactory queueClientFactory)
    {
        private static readonly HttpClient HttpClient = new();
        private readonly QueueClient _weatherQueueClient = queueClientFactory.GetQueueClient("weather-jobs");

        [Function(nameof(StartWeatherJob))]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            logger.LogInformation("StartWeatherJob function triggered.");
            logger.LogInformation($"Using queue name: {_weatherQueueClient.Name}");

            // Fetch weather data from Buienradar
            const string apiUrl = "https://data.buienradar.nl/2.0/feed/json";
            var response = await HttpClient.GetAsync(apiUrl);
            logger.LogInformation($"Received response from Buienradar: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"Failed to fetch weather data: {response.ReasonPhrase}");
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Failed to fetch weather data");
                return badRequestResponse;
            }

            var weatherDataJson = await response.Content.ReadAsStringAsync();

            // Add raw weather data to the queue
            await _weatherQueueClient.CreateIfNotExistsAsync();
            var base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(weatherDataJson));
            await _weatherQueueClient.SendMessageAsync(base64Message);
            logger.LogInformation($"Added raw weather data to the queue: {base64Message}");

            var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await successResponse.WriteStringAsync("Weather data added to the queue.");
            return successResponse;
        }
    }
}