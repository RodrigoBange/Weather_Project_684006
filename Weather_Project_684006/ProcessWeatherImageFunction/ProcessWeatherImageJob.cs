using Azure.Storage.Blobs;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Weather_Project_684006.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using Weather_Project_684006.Utilities;

namespace Weather_Project_684006.ProcessWeatherImageFunction
{
    public class ProcessWeatherImageJob(
        ILogger<ProcessWeatherImageJob> logger,
        BlobServiceClient blobServiceClient,
        HttpClient httpClient)
    {
        [Function(nameof(ProcessWeatherImageJob))]
        public async Task Run([QueueTrigger("image-processing-jobs", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            logger.LogInformation("Processing image job: {MessageText}", message.MessageText);

            try
            {
                // Deserialize the incoming message to extract jobId and station data
                var payload = JsonConvert.DeserializeObject<dynamic>(message.MessageText);
                string jobId = payload?.JobId?.ToString();
                var stationDataJson = payload?.StationData?.ToString();

                // Validate that jobId and station data are present
                if (string.IsNullOrEmpty(jobId) || string.IsNullOrEmpty(stationDataJson))
                {
                    logger.LogError("Invalid payload: missing jobId or station data.");
                    return;
                }

                // Deserialize the station data into a WeatherStation object
                var stationData = JsonConvert.DeserializeObject<WeatherStation>(stationDataJson);
                if (stationData == null)
                {
                    logger.LogError("Failed to deserialize station data.");
                    return;
                }

                // Generate an image with the provided station data
                var outputImagePath = await GenerateWeatherImage(stationData);
                logger.LogInformation("Using provided jobId: {JobId}", jobId);

                // Upload the generated image to Blob Storage
                await UploadImageToBlobStorage(outputImagePath, jobId, stationData.StationName);

                // Clean up the generated image file after successful upload
                if (File.Exists(outputImagePath))
                {
                    File.Delete(outputImagePath);
                }
            }
            catch (Exception ex)
            {
                // Log any unexpected errors that occur during the processing
                logger.LogError(ex, "An error occurred while processing the image job.");
            }
        }

        private async Task<string> GenerateWeatherImage(WeatherStation stationData)
        {
            try
            {
                // Fetch a random background image from the specified URL
                var picsumUrl = "https://picsum.photos/800/400";
                var response = await httpClient.GetAsync(picsumUrl);

                // Check if the response is successful, otherwise log an error and throw an exception
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Error fetching image from Lorem Picsum: {StatusCode}", response.StatusCode);
                    throw new HttpRequestException("Failed to fetch Lorem Picsum image.");
                }

                // Save the downloaded image to a temporary file
                var tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".jpg");
                await using (var fileStream = File.Create(tempFilePath))
                {
                    await response.Content.CopyToAsync(fileStream);
                }

                // Create a new output image file path
                var outputImagePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".png");

                // Load the downloaded image and draw text on it
                using (var image = await Image.LoadAsync(tempFilePath))
                {
                    var font = SystemFonts.CreateFont("Arial", 36);
                    image.Mutate(ctx =>
                    {
                        ctx.DrawText($"Station: {stationData.StationName}", font, Color.White, new PointF(20, 50));
                        ctx.DrawText($"Feel Temperature: {stationData.FeelTemperature}°C", font, Color.White, new PointF(20, 100));
                        ctx.DrawText($"Ground Temperature: {stationData.GroundTemperature}°C", font, Color.White, new PointF(20, 150));
                    });

                    // Save the modified image as a PNG
                    await image.SaveAsPngAsync(outputImagePath);
                }

                // Clean up the temporary downloaded image file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }

                return outputImagePath;
            }
            catch (Exception ex)
            {
                // Log any errors that occur during image generation
                logger.LogError(ex, "An error occurred while generating the weather image.");
                throw;
            }
        }

        private async Task UploadImageToBlobStorage(string imagePath, string jobId, string stationName)
        {
            // Sanitize the station name for use in the blob file name
            var sanitizedStationName = FileNameSanitizer.Sanitize(stationName);

            // Construct the blob name with jobId and sanitized station name
            var blobName = $"{jobId}/station-{sanitizedStationName}-{DateTime.UtcNow:yyyyMMddHHmmss}.png";
            var containerClient = blobServiceClient.GetBlobContainerClient("weather-images");

            // Ensure the blob container exists
            await containerClient.CreateIfNotExistsAsync();

            // Create a BlobClient for the specific blob
            var blobClient = containerClient.GetBlobClient(blobName);

            try
            {
                // Upload the image file to Blob Storage
                await using var stream = File.OpenRead(imagePath);
                await blobClient.UploadAsync(stream, true);
                logger.LogInformation("Uploaded image for job {JobId}, station {StationName}, to Blob Storage.", jobId, stationName);
            }
            catch (Exception ex)
            {
                // Log any errors that occur during the upload process
                logger.LogError(ex, "An error occurred while uploading the image to Blob Storage.");
                throw;
            }
        }
    }
}