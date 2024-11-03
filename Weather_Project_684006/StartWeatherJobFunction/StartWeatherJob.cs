using System.Text;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Weather_Project_684006.Models;

namespace Weather_Project_684006.StartWeatherJobFunction;

public class StartWeatherJob(ILogger<StartWeatherJob> logger, QueueClient queueClient)
{
    private static readonly HttpClient HttpClient = new();

    [Function("StartWeatherJob")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        logger.LogInformation("StartWeatherJob function triggered.");

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
        var weatherData = ParseWeatherData(weatherDataJson);

        // Check if there is any weather data
        if (!weatherData.Any())
        {
            logger.LogWarning("No weather data found.");
            var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
            await notFoundResponse.WriteStringAsync("No weather data found.");
            return notFoundResponse;
        }

        // Add weather data to the queue
        await queueClient.CreateIfNotExistsAsync();
        foreach (var station in weatherData)
        {
            var message = JsonConvert.SerializeObject(station);
            var base64Message = Convert.ToBase64String(Encoding.UTF8.GetBytes(message));
            await queueClient.SendMessageAsync(base64Message);
            logger.LogInformation($"Added message to the queue: {base64Message}");
        }

        var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await successResponse.WriteStringAsync("Weather data added to the queue.");
        return successResponse;
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