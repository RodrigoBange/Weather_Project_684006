using System.Text;
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

namespace Weather_Project_684006.ProcessWeatherImageFunction
{
    public class ProcessWeatherImageJob(ILogger<ProcessWeatherImageJob> logger, BlobServiceClient blobServiceClient)
    {
        [Function(nameof(ProcessWeatherImageJob))]
        public async Task Run([QueueTrigger("image-processing-jobs", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            logger.LogInformation($"Processing image job: {message.MessageText}");

            var weatherData = JsonConvert.DeserializeObject<WeatherStation>(message.MessageText);
            if (weatherData == null)
            {
                logger.LogError("Failed to deserialize weather data for image processing.");
                return;
            }

            var outputImagePath = await GenerateWeatherImage(weatherData);
            var jobId = Guid.NewGuid().ToString();
            logger.LogInformation($"Generated jobId: {jobId}");
            await UploadImageToBlobStorage(outputImagePath, jobId, weatherData.StationName);

            // Clean up the generated image file
            if (File.Exists(outputImagePath))
            {
                File.Delete(outputImagePath);
            }
        }

        private async Task<string> GenerateWeatherImage(WeatherStation weatherData)
        {
            var picsumUrl = "https://picsum.photos/800/400";
            var response = await new HttpClient().GetAsync(picsumUrl);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError($"Error fetching image from Lorem Picsum: {response.StatusCode}");
                throw new Exception("Failed to fetch Lorem Picsum image.");
            }

            var tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".jpg");
            await using (var fileStream = File.Create(tempFilePath))
            {
                await response.Content.CopyToAsync(fileStream);
            }

            var outputImagePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");

            using (var image = await Image.LoadAsync(tempFilePath))
            {
                var font = SystemFonts.CreateFont("Arial", 36);
                image.Mutate(ctx =>
                {
                    ctx.DrawText($"Station: {weatherData.StationName}", font, Color.White, new PointF(20, 50));
                    ctx.DrawText($"Feel Temperature: {weatherData.FeelTemperature}°C", font, Color.White, new PointF(20, 100));
                    ctx.DrawText($"Ground Temperature: {weatherData.GroundTemperature}°C", font, Color.White, new PointF(20, 150));
                });

                await image.SaveAsPngAsync(outputImagePath);
            }

            // Clean up the temporary file after loading it into ImageSharp
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            return outputImagePath;
        }

        private async Task UploadImageToBlobStorage(string imagePath, string jobId, string stationName)
        {
            var containerClient = blobServiceClient.GetBlobContainerClient("weather-images");
            await containerClient.CreateIfNotExistsAsync();

            // Include jobId in the blob name for better organization
            var blobName = $"{jobId}/station-{stationName}-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
            var blobClient = containerClient.GetBlobClient(blobName);

            await using var stream = File.OpenRead(imagePath);
            await blobClient.UploadAsync(stream, true);
            logger.LogInformation($"Uploaded image for job {jobId}, station {stationName}, to Blob Storage.");
        }
    }
}
