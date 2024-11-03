using System;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weather_Project_684006.ProcessWeatherImageFunction;
using Weather_Project_684006.StartWeatherJobFunction;

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
        
        // Register BlobServiceClient for blob storage operations
        services.AddSingleton(_ =>
        {
            var blobConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            return new BlobServiceClient(blobConnectionString);
        });

        // Register StartWeatherJob as a transient service
        services.AddTransient<StartWeatherJob>();
        
        // Register ProcessWeatherImageJob as a transient service
        services.AddTransient<ProcessWeatherImageJob>();
    })
    .Build();

host.Run();