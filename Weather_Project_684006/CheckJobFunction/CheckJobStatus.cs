using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Weather_Project_684006.Utilities;

namespace Weather_Project_684006.CheckJobFunction;

public class CheckJobStatus(
    ILogger<CheckJobStatus> logger,
    BlobServiceClient blobServiceClient,
    SasTokenGenerator sasTokenGenerator)
{
    [Function("CheckJobStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{jobId}")] HttpRequestData req,
        string jobId)
    {
        logger.LogInformation($"Checking status for jobId: {jobId}");

        const string containerName = "weather-images";
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var sasUrls = new List<string>();

        // Fetch blobs using the jobId as a prefix
        var blobs = containerClient.GetBlobsAsync(prefix: $"{jobId}/");

        await foreach (var blob in blobs)
        {
            logger.LogInformation($"Found blob: {blob.Name}");

            if (!blob.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

            // Generate SAS URI for each blob
            var sasUri = sasTokenGenerator.GenerateBlobSasUri(containerName, blob.Name, TimeSpan.FromHours(1));
            sasUrls.Add(sasUri.ToString());
        }

        if (sasUrls.Count == 0)
        {
            logger.LogInformation($"No images found for jobId {jobId}. The job might still be processing.");
            var ongoingResponse = req.CreateResponse(System.Net.HttpStatusCode.Accepted);
            await ongoingResponse.WriteStringAsync($"Job with jobId {jobId} is still processing or no images have been generated yet.");
            return ongoingResponse;
        }

        // Return the list of SAS URLs as JSON if processing is complete
        var successResponse = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await successResponse.WriteAsJsonAsync(new { JobId = jobId, ImageUrls = sasUrls });
        return successResponse;
    }
}