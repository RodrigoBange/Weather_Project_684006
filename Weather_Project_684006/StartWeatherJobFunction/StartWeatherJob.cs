using System.Text;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Weather_Project_684006.Factories;

namespace Weather_Project_684006.StartWeatherJobFunction
{
    public class StartWeatherJob(
        ILogger<StartWeatherJob> logger,
        QueueClientFactory queueClientFactory,
        HttpClient httpClient)
    {
        private readonly QueueClient _weatherQueueClient = queueClientFactory.GetQueueClient("weather-jobs");

        [Function(nameof(StartWeatherJob))]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            logger.LogInformation("StartWeatherJob function triggered. Using queue: {QueueName}", _weatherQueueClient.Name);

            try
            {
                // Fetch weather data from Buienradar
                const string apiUrl = "https://data.buienradar.nl/2.0/feed/json";
                var response = await httpClient.GetAsync(apiUrl);
                logger.LogInformation("Received response from Buienradar with status code: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Failed to fetch weather data: {Reason}", response.ReasonPhrase);
                    var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Failed to fetch weather data.");
                    return badRequestResponse;
                }

                var weatherDataJson = await response.Content.ReadAsStringAsync();

                // Generate a unique jobId
                var jobId = Guid.NewGuid().ToString();

                // Create payload with jobId and weather data
                var payload = new
                {
                    JobId = jobId,
                    WeatherData = weatherDataJson
                };

                // Add payload to the queue
                await _weatherQueueClient.CreateIfNotExistsAsync();
                var base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload)));
                await _weatherQueueClient.SendMessageAsync(base64Message);
                logger.LogInformation("Added weather data to the queue with jobId: {JobId}", jobId);

                // Return status URL
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
            catch (Exception ex)
            {
                logger.LogError(ex, "An unexpected error occurred while processing the request.");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while processing the request.");
                return errorResponse;
            }
        }
    }
}