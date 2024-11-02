using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Weather_Project_684006.Functions;

public class ProcessWeatherImageJob
{
    private readonly ILogger<ProcessWeatherImageJob> _logger;

    public ProcessWeatherImageJob(ILogger<ProcessWeatherImageJob> logger)
    {
        _logger = logger;
    }

    [Function("ProcessWeatherImageJob")]
    public IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        return new OkObjectResult("Welcome to Azure Functions!");
        
    }

}