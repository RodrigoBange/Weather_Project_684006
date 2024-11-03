using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Weather_Project_684006.Models;

namespace Weather_Project_684006.Functions
{
    public class ProcessWeatherJob
    {
        private readonly ILogger<ProcessWeatherJob> _logger;
        private readonly BlobServiceClient _blobServiceClient;
        private static readonly HttpClient HttpClient = new();

        public ProcessWeatherJob(ILogger<ProcessWeatherJob> logger, BlobServiceClient blobServiceClient)
        {
            _logger = logger;
            _blobServiceClient = blobServiceClient;
        }

        [Function(nameof(ProcessWeatherJob))]
        public async Task Run([QueueTrigger("weather-jobs", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation($"Processing message: {message.MessageText}");

            try
            {
                // Deserialize the message and check for null weatherData
                var weatherData = JsonConvert.DeserializeObject<WeatherStation>(message.MessageText);
                if (weatherData == null)
                {
                    _logger.LogError("Failed to deserialize weather data.");
                    return;
                }

                // Generate a unique jobId
                var jobId = Guid.NewGuid().ToString();
                _logger.LogInformation($"Generated jobId: {jobId}");

                // Fetch a random image from Lorem Picsum
                var imagePath = await FetchLoremPicsumImage();
                if (imagePath == null)
                {
                    _logger.LogError("Failed to fetch Lorem Picsum image.");
                    return;
                }

                // Generate the image by overlaying weather data
                var outputImagePath = await GenerateWeatherImage(weatherData, imagePath);

                // Upload the image to Azure Blob Storage with the jobId
                await UploadImageToBlobStorage(outputImagePath, jobId, weatherData.StationName);

                _logger.LogInformation($"Image for {weatherData.StationName} generated and uploaded with jobId: {jobId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred: {ex.Message}");
                throw;
            }
        }

        private static async Task<string?> FetchLoremPicsumImage()
        {
            var picsumUrl = "https://picsum.photos/800/400";

            try
            {
                var response = await HttpClient.GetAsync(picsumUrl);
                if (response.IsSuccessStatusCode)
                {
                    var imagePath = Path.GetTempFileName() + ".jpg";
                    await using var stream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = File.Create(imagePath);
                    await stream.CopyToAsync(fileStream);
                    return imagePath;
                }
                else
                {
                    Console.WriteLine($"Error fetching image: {response.StatusCode}");
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine($"Request error: {e.Message}");
            }

            return null;
        }

        private static async Task<string> GenerateWeatherImage(WeatherStation weatherData, string imagePath)
        {
            using var image = await Image.LoadAsync(imagePath);

            // Define font and drawing options
            var font = SystemFonts.CreateFont("Arial", 36);

            // Draw text onto the image
            image.Mutate(ctx =>
            {
                ctx.DrawText($"Station: {weatherData.StationName}", font, Color.White, new PointF(20, 50));
                ctx.DrawText($"Feel Temperature: {weatherData.FeelTemperature}°C", font, Color.White, new PointF(20, 100));
                ctx.DrawText($"Ground Temperature: {weatherData.GroundTemperature}°C", font, Color.White, new PointF(20, 150));
            });

            // Save the modified image to a new file
            var outputImagePath = Path.GetTempFileName() + ".png";
            await image.SaveAsPngAsync(outputImagePath);
            return outputImagePath;
        }

        private async Task UploadImageToBlobStorage(string imagePath, string jobId, string? stationName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("weather-images");
            await containerClient.CreateIfNotExistsAsync();

            var blobName = $"{jobId}/station-{stationName}-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
            var blobClient = containerClient.GetBlobClient(blobName);

            await using var stream = File.OpenRead(imagePath);
            await blobClient.UploadAsync(stream, true);
        }
    }
}