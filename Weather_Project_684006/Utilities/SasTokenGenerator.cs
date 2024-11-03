using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace Weather_Project_684006.Utilities;

public class SasTokenGenerator(BlobServiceClient blobServiceClient)
{
    public Uri GenerateBlobSasUri(string containerName, string blobName, TimeSpan validDuration)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (blobClient.CanGenerateSasUri)
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.Add(validDuration)
            };
                
            sasBuilder.SetPermissions(BlobSasPermissions.Read); // Set read permission

            return blobClient.GenerateSasUri(sasBuilder);
        }
        else
        {
            throw new InvalidOperationException("Cannot generate SAS token for this blob.");
        }
    }
}