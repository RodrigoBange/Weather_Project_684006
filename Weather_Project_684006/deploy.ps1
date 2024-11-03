# Variables
$resourceGroupName = "weather-project-684006-resource-group"
$location = "westeurope"
$templateFile = "./main.bicep"
$deploymentName = "template-deployment"

# Login check
$accountInfo = az account show 2>$null
if (-not $accountInfo) {
    Write-Host "Azure login required. Redirecting to login..."
    az login
}

# Ensure resource group exists
az group show --name $resourceGroupName --output none 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Resource group $resourceGroupName not found. Creating it in $location..."
    az group create --name $resourceGroupName --location $location | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to create resource group. Check your permissions."
        exit 1
    }
} else {
    Write-Host "Resource group $resourceGroupName found."
}

# Deploy Bicep template to resource group
Write-Host "Starting deployment with Bicep template..."
az deployment group create --resource-group $resourceGroupName --template-file $templateFile --parameters location=$location --name $deploymentName

if ($LASTEXITCODE -eq 0) {
    Write-Host "Deployment completed successfully."
} else {
    Write-Host "Deployment failed. Fetching logs for troubleshooting..."
    az deployment operation group list --resource-group $resourceGroupName --name $deploymentName
    exit 1
}

# Get the Function App name
$functionAppName = az functionapp list --resource-group $resourceGroupName --query "[].name" -o tsv | Select-Object -First 1

if (-not $functionAppName) {
    Write-Host "Error: No Function App found in the resource group $resourceGroupName."
    exit 1
}

# Check function app status and start if necessary
Write-Host "Checking function app status for $functionAppName..."
$functionAppState = az functionapp show --name $functionAppName --resource-group $resourceGroupName --query "state" -o tsv
if ($functionAppState -ne "Running") {
    Write-Host "Function app $functionAppName is not running. Starting..."
    az functionapp start --name $functionAppName --resource-group $resourceGroupName
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Error: Failed to start function app. Check Azure portal."
        exit 1
    }
}

# Ensure the Function App is running
$functionAppState = az functionapp show --name $functionAppName --resource-group $resourceGroupName --query "state" -o tsv
if ($functionAppState -ne "Running") {
    Write-Host "Function app $functionAppName is not running. Starting..."
    az functionapp start --name $functionAppName --resource-group $resourceGroupName

    # Wait for the function app to start
    Start-Sleep -Seconds 15  # Increased wait time
    $functionAppState = az functionapp show --name $functionAppName --resource-group $resourceGroupName --query "state" -o tsv
    if ($functionAppState -ne "Running") {
        Write-Host "Function app $functionAppName is still not running. Exiting."
        exit 1
    }
}

# Publish Azure Functions to the Function App with retry
$maxRetries = 3
$retryDelay = 5  # seconds

for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
    Write-Host "Attempting to publish Azure Functions to $functionAppName... (Attempt $attempt of $maxRetries)"

    func azure functionapp publish $functionAppName --build-native-deps --force --verbose

    if ($LASTEXITCODE -eq 0) {
        Write-Host "Azure Functions published successfully."
        break
    } else {
        Write-Host "Publishing failed. Retrying in $retryDelay seconds..."
        Start-Sleep -Seconds $retryDelay

        # Check the current status of the function app again before retrying
        $functionAppState = az functionapp show --name $functionAppName --resource-group $resourceGroupName --query "state" -o tsv
        if ($functionAppState -ne "Running") {
            Write-Host "Function app $functionAppName is not in a running state. Exiting."
            exit 1
        }

        Write-Host "Check function app logs for more details on the error."
    }

    if ($attempt -eq $maxRetries) {
        Write-Host "Max retries reached. Publishing failed."
        exit 1
    }
}

# Verify deployed Azure Functions
Write-Host "Verifying deployed functions..."
$deployedFunctions = az functionapp function list --name $functionAppName --resource-group $resourceGroupName -o table

if ($deployedFunctions) {
    Write-Host "Successfully deployed the following functions:"
    Write-Host $deployedFunctions
} else {
    Write-Host "Error: No functions detected after deployment. Review publishing logs."
    exit 1
}