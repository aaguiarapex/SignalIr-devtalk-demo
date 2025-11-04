targetScope = 'subscription'

@description('Resource group name to create (lowercase letters, numbers, and hyphens).')
param resourceGroupName string

@description('Azure region for the resource group and all resources.')
param location string

@description('Base name prefix for all Azure resources. Use lowercase letters and numbers.')
param baseName string

@description('App Service plan SKU (e.g., F1, B1, S1).')
param appServiceSkuName string = 'B1'

@description('App Service plan tier (e.g., Free, Basic, Standard).')
param appServiceSkuTier string = 'Basic'

resource appRg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: resourceGroupName
  location: location
}

module app 'app.bicep' = {
  name: '${baseName}-app'
  scope: resourceGroup(appRg.name)
  params: {
    baseName: baseName
    location: location
    appServiceSkuName: appServiceSkuName
    appServiceSkuTier: appServiceSkuTier
  }
}

output resourceGroup string = appRg.name
output webAppName string = app.outputs.webAppName
output webAppDefaultHostName string = app.outputs.webAppDefaultHostName
output signalRConnectionString string = app.outputs.signalRConnectionString
