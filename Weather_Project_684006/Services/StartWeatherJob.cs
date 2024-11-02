using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Weather_Project_684006.Services;

public class StartWeatherJob
{
    private readonly ILogger<StartWeatherJob> _logger;

    public StartWeatherJob(ILogger<StartWeatherJob> logger)
    {
        _logger = logger;
    }

    [Function("StartWeatherJob")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
        
    }

}