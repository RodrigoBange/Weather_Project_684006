using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Weather_Project_684006.Utilities;

public static class AuthHelper
{
    public static bool ValidateApiKey(HttpRequestData req, string expectedApiKey, ILogger logger)
    {
        if (req.Headers.TryGetValues("Authorization", out var authHeaders) &&
            authHeaders.FirstOrDefault() == $"Bearer {expectedApiKey}") return true;
        logger.LogWarning("Unauthorized access attempt.");
        return false;
    }
}