# Weather_Project_684006

Assignment for Server Side Programming - 684006

## Overview
This project uses Azure Functions for processing and handling weather data. The project includes multiple Azure Functions that interact with Azure Storage, process data, generate images, and provides a status-check endpoint.

## Features
- **Fetch Weather Data**: `StartWeatherJob` function fetches weather data from an external source and queues it for processing.
- **Process Weather Data**: `ProcessWeatherJob` function processes queued weather data and sends it to another queue for image generation.
- **Generate Weather Images**: `ProcessWeatherImageJob` creates visual representations of weather data and uploads them to an Azure Blob Storage container.
- **Check Job Status**: `CheckJobStatus` and `GetWeatherImageJob` functions provide status updates and access to generated images via SAS URLs.

## Project Structure
- **Factories**: Contains `QueueClientFactory` for creating instances of `QueueClient`.
- **Utilities**: Contains helper classes like `AuthHelper`, `FileNameSanitizer`, and `SasTokenGenerator`.
- **Functions**:
  - `StartWeatherJobFunction`: Triggers the initial process of fetching and queuing weather data.
  - `ProcessWeatherFunction`: Processes weather data from the queue and triggers image generation.
  - `ProcessWeatherImageFunction`: Generates images from the weather data and uploads them to Blob Storage.
  - `CheckJobFunction`: Provides status updates on processed weather jobs.
  - `GetWeatherImageFunction`: Returns generated images with SAS tokens for secure access.
