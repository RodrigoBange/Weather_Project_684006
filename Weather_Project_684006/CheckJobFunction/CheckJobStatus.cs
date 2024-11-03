using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using Weather_Project_684006.Utilities;

namespace Weather_Project_684006.CheckJobFunction
{
    public class CheckJobStatus(
        ILogger<CheckJobStatus> logger,
        BlobServiceClient blobServiceClient,
        SasTokenGenerator sasTokenGenerator)
    {
        private const string ContainerName = "weather-images";

        [Function("CheckJobStatus")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "status/{jobId}")] HttpRequestData req,
            string jobId)
        {
            logger.LogInformation("Checking status for jobId: {JobId}", jobId);

            try
            {
                // Initialize container client
                var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
                var sasUrls = new List<string>();

                // Fetch blobs using jobId as a prefix
                logger.LogInformation("Fetching blobs with prefix: {JobId}", jobId);
                var blobs = containerClient.GetBlobsAsync(prefix: $"{jobId}/");

                await foreach (var blob in blobs.ConfigureAwait(false))
                {
                    logger.LogInformation("Found blob: {BlobName}", blob.Name);

                    // Filter for PNG images only
                    if (!blob.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogDebug("Skipping non-PNG blob: {BlobName}", blob.Name);
                        continue;
                    }

                    // Generate SAS URI for each qualifying blob
                    var sasUri = sasTokenGenerator.GenerateBlobSasUri(ContainerName, blob.Name, TimeSpan.FromHours(1));
                    sasUrls.Add(sasUri.ToString());
                }

                // Handle case where no images are found
                if (sasUrls.Count == 0)
                {
                    logger.LogWarning("No images found for jobId: {JobId}. The job might still be processing.", jobId);
                    var ongoingResponse = req.CreateResponse(HttpStatusCode.Accepted);
                    await ongoingResponse.WriteStringAsync($"Job with jobId {jobId} is still processing or no images have been generated yet. Try reloading the page.").ConfigureAwait(false);
                    return ongoingResponse;
                }

                // Return the list of SAS URLs as a JSON response
                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                await successResponse.WriteAsJsonAsync(new { JobId = jobId, ImageUrls = sasUrls }).ConfigureAwait(false);
                return successResponse;
            }
            catch (Exception ex)
            {
                // Handle unexpected errors and return a 500 response
                logger.LogError(ex, "An error occurred while checking the status for jobId: {JobId}", jobId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while processing your request.").ConfigureAwait(false);
                return errorResponse;
            }
        }
    }
}