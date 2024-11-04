using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using Weather_Project_684006.Utilities;

namespace Weather_Project_684006.GetWeatherImageFunction
{
    public class GetWeatherImageJob(
        ILogger<GetWeatherImageJob> logger,
        BlobServiceClient blobServiceClient,
        SasTokenGenerator sasTokenGenerator)
    {
        private const string ContainerName = "weather-images";
        private readonly string _expectedApiKey = Environment.GetEnvironmentVariable("MY_API_KEY");

        [Function("GetWeatherImageJob")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "image/{jobId}")] HttpRequestData req,
            string jobId)
        {
            // Validate the API key
            if (!AuthHelper.ValidateApiKey(req, _expectedApiKey, logger))
            {
                var unauthorizedResponse = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Unauthorized");
                return unauthorizedResponse;
            }
            
            logger.LogInformation("Generating SAS token for job ID: {JobId}", jobId);

            var sasUrls = new List<string>();
            var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);

            try
            {
                // Fetch blobs using the jobId as a prefix
                logger.LogInformation("Fetching blobs with prefix: {JobId}", jobId);
                var blobs = containerClient.GetBlobsAsync(prefix: $"{jobId}/");

                await foreach (var blob in blobs.ConfigureAwait(false))
                {
                    if (!blob.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    {
                        logger.LogDebug("Skipping non-PNG blob: {BlobName}", blob.Name);
                        continue;
                    }

                    logger.LogInformation("Found blob: {BlobName}", blob.Name);

                    // Generate SAS URI for each blob
                    var sasUri = sasTokenGenerator.GenerateBlobSasUri(ContainerName, blob.Name, TimeSpan.FromHours(1));
                    sasUrls.Add(sasUri.ToString());
                }

                // Handle the case where no images are found
                if (sasUrls.Count == 0)
                {
                    logger.LogWarning("No image found for job ID: {JobId}", jobId);
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"No image found for job ID: {jobId}").ConfigureAwait(false);
                    return notFoundResponse;
                }

                // Return the list of SAS URLs as JSON
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(sasUrls).ConfigureAwait(false);
                return response;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while processing the request for job ID: {JobId}", jobId);
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("An error occurred while processing your request.").ConfigureAwait(false);
                return errorResponse;
            }
        }
    }
}