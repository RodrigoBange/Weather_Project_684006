using System;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weather_Project_684006.Functions;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Register Application Insights for telemetry
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register QueueClient for the "weather-jobs" queue
        services.AddSingleton(_ => new QueueClient(
            Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
            "weather-jobs"
        ));

        // Register StartWeatherJob as a transient service
        services.AddTransient<StartWeatherJob>();

        // You can register other services here as needed
    })
    .Build();

host.Run();