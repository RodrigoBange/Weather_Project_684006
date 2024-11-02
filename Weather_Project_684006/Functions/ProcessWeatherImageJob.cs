using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;

namespace Weather_Project_684006.Functions;

public class ProcessWeatherImageJob(ILogger<ProcessWeatherImageJob> logger, BlobServiceClient blobServiceClient)
{
    [Function("ProcessWeatherImageJob")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "image/{jobId}")] HttpRequestData req,
        string jobId)
    {
        logger.LogInformation($"Processing job ID: {jobId}");

        var containerClient = blobServiceClient.GetBlobContainerClient("weather-images");
        var blobs = containerClient.GetBlobsAsync(prefix: jobId);

        await foreach (var blob in blobs)
        {
            logger.LogInformation($"Found blob: {blob.Name}");

            if (!blob.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            logger.LogInformation($"Image found for job ID: {jobId}");

            var blobClient = containerClient.GetBlobClient(blob.Name);
            var memoryStream = new MemoryStream();

            await blobClient.DownloadToAsync(memoryStream);
            memoryStream.Position = 0;

            var response = req.CreateResponse();
            response.Headers.Add("Content-Type", "image/png");
            response.Headers.Add("Content-Disposition", $"inline; filename=\"{Path.GetFileName(blob.Name)}\"");
            await response.WriteBytesAsync(memoryStream.ToArray());
                    
            return response;
        }

        logger.LogError($"No image found for job ID: {jobId}");
        var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
        await notFoundResponse.WriteStringAsync($"No image found for job ID: {jobId}");
        return notFoundResponse;
    }
}