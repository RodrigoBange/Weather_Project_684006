@description('The location for all resources')
param location string = resourceGroup().location

@description('The name for the storage account')
param storageAccountName string = 'funcstor${uniqueString(resourceGroup().id)}'

@description('The name for the function app')
param functionAppName string = 'func-${uniqueString(resourceGroup().id)}'

@description('The hosting plan for the function app (consumption, basic, etc.)')
param appServicePlanSkuName string = 'Y1'

@description('The runtime stack for the function app')
param functionAppRuntime string = 'dotnet-isolated'

@description('The version of the runtime')
param functionAppRuntimeVersion string = '~4'

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
  }
}

// Retrieve the storage account connection string
var storageAccountKeys = storageAccount.listKeys().keys[0].value
var storageAccountConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccountKeys};EndpointSuffix=core.windows.net'

// App Service Plan (Consumption plan)
resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${functionAppName}-plan'
  location: location
  sku: {
    name: appServicePlanSkuName
    tier: 'Dynamic'
  }
}

// Define the file service and file share for the storage account
resource fileService 'Microsoft.Storage/storageAccounts/fileServices@2022-09-01' = {
  name: 'default'
  parent: storageAccount
}

resource contentFileShare 'Microsoft.Storage/storageAccounts/fileServices/shares@2022-09-01' = {
  name: 'content-share'
  parent: fileService
}

// Function App
resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: storageAccountConnectionString
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: functionAppRuntime
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: functionAppRuntimeVersion
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: storageAccountConnectionString
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: 'content-share'
        }
      ]
    }
  }
}

// Define the storage queues as child resources of the storage account
resource queueServices 'Microsoft.Storage/storageAccounts/queueServices@2022-09-01' = {
  name: 'default'
  parent: storageAccount
}

resource imageProcessingQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2022-09-01' = {
  name: 'weather-jobs'
  parent: queueServices
}

// Define the blob services for the storage account
resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2022-09-01' = {
  name: 'default'
  parent: storageAccount
}

// Define the blob container
resource imageStorageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2022-09-01' = {
  name: 'weather-images'
  parent: blobService
  properties: {
    publicAccess: 'Blob'
  }
}

// Outputs for logging during deployment
output functionAppDefaultHostName string = functionApp.properties.defaultHostName
output storageAccountPrimaryEndpoint string = storageAccount.properties.primaryEndpoints.blob
output storageAccountConnectionString string = storageAccountConnectionString