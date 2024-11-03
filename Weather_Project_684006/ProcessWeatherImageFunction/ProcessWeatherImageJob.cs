using Azure.Storage.Blobs;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Weather_Project_684006.Models;
using System.Threading.Tasks;
using System.IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;

namespace Weather_Project_684006.ProcessWeatherImageFunction
{
    public class ProcessWeatherImageJob
    {
        private readonly ILogger<ProcessWeatherImageJob> _logger;
        private readonly BlobServiceClient _blobServiceClient;

        public ProcessWeatherImageJob(ILogger<ProcessWeatherImageJob> logger, BlobServiceClient blobServiceClient)
        {
            _logger = logger;
            _blobServiceClient = blobServiceClient;
        }

        [Function(nameof(ProcessWeatherImageJob))]
        public async Task Run([QueueTrigger("image-processing-jobs", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            _logger.LogInformation($"Processing image job: {message.MessageText}");

            var payload = JsonConvert.DeserializeObject<dynamic>(message.MessageText);
            string jobId = payload?.JobId;
            var stationData = JsonConvert.DeserializeObject<WeatherStation>(payload?.StationData.ToString());

            if (stationData == null || string.IsNullOrEmpty(jobId))
            {
                _logger.LogError("Failed to deserialize station data or missing jobId for image processing.");
                return;
            }

            var outputImagePath = await GenerateWeatherImage(stationData);
            _logger.LogInformation($"Using provided jobId: {jobId}");
            await UploadImageToBlobStorage(outputImagePath, jobId, stationData.StationName);

            // Clean up the generated image file
            if (File.Exists(outputImagePath))
            {
                File.Delete(outputImagePath);
            }
        }

        private async Task<string> GenerateWeatherImage(WeatherStation stationData)
        {
            var picsumUrl = "https://picsum.photos/800/400";
            var response = await new HttpClient().GetAsync(picsumUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Error fetching image from Lorem Picsum: {response.StatusCode}");
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
                    ctx.DrawText($"Station: {stationData.StationName}", font, Color.White, new PointF(20, 50));
                    ctx.DrawText($"Feel Temperature: {stationData.FeelTemperature}°C", font, Color.White, new PointF(20, 100));
                    ctx.DrawText($"Ground Temperature: {stationData.GroundTemperature}°C", font, Color.White, new PointF(20, 150));
                });

                await image.SaveAsPngAsync(outputImagePath);
            }

            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            return outputImagePath;
        }

        private async Task UploadImageToBlobStorage(string imagePath, string jobId, string stationName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("weather-images");
            await containerClient.CreateIfNotExistsAsync();

            var blobName = $"{jobId}/station-{stationName.Replace(" ", "-")}-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
            var blobClient = containerClient.GetBlobClient(blobName);

            await using var stream = File.OpenRead(imagePath);
            await blobClient.UploadAsync(stream, true);
            _logger.LogInformation($"Uploaded image for job {jobId}, station {stationName}, to Blob Storage.");
        }
    }
}