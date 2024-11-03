using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Weather_Project_684006.Utilities;

namespace Weather_Project_684006.ProcessWeatherImageFunction
{
    public class ProcessWeatherImageJob(
        ILogger<ProcessWeatherImageJob> logger,
        BlobServiceClient blobServiceClient,
        SasTokenGenerator sasTokenGenerator)
    {
        [Function("ProcessWeatherImageJob")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "image/{jobId}")] HttpRequestData req,
            string jobId)
        {
            logger.LogInformation($"Processing job Id: {jobId}");

            const string containerName = "weather-images";
            var sasUrls = new List<string>();

            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobs = containerClient.GetBlobsAsync(prefix: jobId);

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
                logger.LogError($"No image found for job Id: {jobId}");
                var notFoundResponse = req.CreateResponse(System.Net.HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync($"No image found for job Id: {jobId}");
                return notFoundResponse;
            }

            // Return the list of SAS URLs as JSON
            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await response.WriteAsJsonAsync(sasUrls);
            return response;
        }
    }
}