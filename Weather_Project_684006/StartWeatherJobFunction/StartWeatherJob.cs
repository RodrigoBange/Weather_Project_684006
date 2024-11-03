using System.Text;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Weather_Project_684006.Factories;

namespace Weather_Project_684006.StartWeatherJobFunction
{
    public class StartWeatherJob
    {
        private static readonly HttpClient HttpClient = new();
        private readonly ILogger<StartWeatherJob> _logger;
        private readonly QueueClient _weatherQueueClient;

        public StartWeatherJob(ILogger<StartWeatherJob> logger, QueueClientFactory queueClientFactory)
        {
            _logger = logger;
            _weatherQueueClient = queueClientFactory.GetQueueClient("weather-jobs");
        }

        [Function(nameof(StartWeatherJob))]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("StartWeatherJob function triggered.");
            _logger.LogInformation($"Using queue name: {_weatherQueueClient.Name}");

            // Step 1: Fetch weather data from Buienradar
            const string apiUrl = "https://data.buienradar.nl/2.0/feed/json";
            var response = await HttpClient.GetAsync(apiUrl);
            _logger.LogInformation($"Received response from Buienradar: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Failed to fetch weather data: {response.ReasonPhrase}");
                var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Failed to fetch weather data");
                return badRequestResponse;
            }

            var weatherDataJson = await response.Content.ReadAsStringAsync();

            // Step 2: Generate a unique jobId
            var jobId = Guid.NewGuid().ToString();

            // Step 3: Create payload with jobId and weather data
            var payload = new
            {
                JobId = jobId,
                WeatherData = weatherDataJson
            };

            // Step 4: Add payload to the queue
            await _weatherQueueClient.CreateIfNotExistsAsync();
            var base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
            await _weatherQueueClient.SendMessageAsync(base64Message);
            _logger.LogInformation($"Added weather data to the queue with jobId: {jobId}");

            // Step 5: Return status URL
            var statusUrl = $"{req.Url.GetLeftPart(UriPartial.Authority)}/api/status/{jobId}";
            var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await successResponse.WriteAsJsonAsync(new
            {
                Message = "Weather data added to the queue.",
                JobId = jobId,
                StatusUrl = statusUrl
            });

            return successResponse;
        }
    }
}