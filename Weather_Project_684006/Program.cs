using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Weather_Project_684006.CheckJobFunction;
using Weather_Project_684006.Factories;
using Weather_Project_684006.StartWeatherJobFunction;
using Weather_Project_684006.ProcessWeatherFunction;
using Weather_Project_684006.ProcessWeatherImageFunction;
using Weather_Project_684006.GetWeatherImageFunction;
using Weather_Project_684006.Utilities;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        // Register Application Insights for telemetry
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Register QueueClient for the "weather-jobs" queue
        services.AddSingleton<QueueClientWeather>(_ => new QueueClientWeather(
            new QueueClient(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
                "weather-jobs"
            )
        ));

        // Register QueueClient for the "image-processing-jobs" queue
        services.AddSingleton<QueueClientImages>(_ => new QueueClientImages(
            new QueueClient(
                Environment.GetEnvironmentVariable("AzureWebJobsStorage"),
                "image-processing-jobs"
            )
        ));
        
        // Register the QueueClientFactory
        services.AddSingleton<QueueClientFactory>();
        
        // Register BlobServiceClient for blob storage operations
        services.AddSingleton(_ =>
        {
            var blobConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            return new BlobServiceClient(blobConnectionString);
        });
        
        // Register SasTokenGenerator as a singleton service
        services.AddSingleton<SasTokenGenerator>();

        // Register StartWeatherJob as a transient service
        services.AddTransient<StartWeatherJob>();

        // Register ProcessWeatherJob as a transient service
        services.AddTransient<ProcessWeatherJob>();
        
        // Register ProcessWeatherImageJob as a transient service
        services.AddTransient<ProcessWeatherImageJob>();

        // Register GetWeatherImageJob as a transient service
        services.AddTransient<GetWeatherImageJob>();
        
        // Register CheckJobStatus as a transient service
        services.AddTransient<CheckJobStatus>();
    })
    .Build();

host.Run();
