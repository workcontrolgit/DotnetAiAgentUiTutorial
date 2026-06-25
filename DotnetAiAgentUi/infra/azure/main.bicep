@description('Azure region for the Azure OpenAI resource.')
param location string = resourceGroup().location

@description('Name of the Azure OpenAI / Azure AI Foundry resource.')
param accountName string

@description('Deployment name used by the application code.')
param deploymentName string = 'gpt-4.1-mini'

@description('Model name to deploy.')
param modelName string = 'gpt-4.1-mini'

@description('Model version. Confirm current regional availability before deployment.')
param modelVersion string = '2025-04-14'

@description('SKU for the Azure OpenAI resource.')
@allowed([
  'S0'
])
param accountSku string = 'S0'

@description('Deployment SKU. For gpt-4.1-mini this is commonly GlobalStandard or Standard, depending on region/quota.')
@allowed([
  'GlobalStandard'
  'Standard'
])
param deploymentSkuName string = 'GlobalStandard'

@description('Deployment capacity units.')
@minValue(1)
param deploymentCapacity int = 1

resource openAi 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: accountName
  location: location
  kind: 'OpenAI'
  sku: {
    name: accountSku
  }
  properties: {
    customSubDomainName: accountName
    publicNetworkAccess: 'Enabled'
  }
}

resource modelDeployment 'Microsoft.CognitiveServices/accounts/deployments@2024-10-01' = {
  name: '${openAi.name}/${deploymentName}'
  sku: {
    name: deploymentSkuName
    capacity: deploymentCapacity
  }
  properties: {
    model: {
      format: 'OpenAI'
      name: modelName
      version: modelVersion
    }
    versionUpgradeOption: 'OnceNewDefaultVersionAvailable'
    currentCapacity: deploymentCapacity
    raiPolicyName: 'Microsoft.Default'
  }
}

output endpoint string = openAi.properties.endpoint
output deployment string = deploymentName
output resourceName string = openAi.name
