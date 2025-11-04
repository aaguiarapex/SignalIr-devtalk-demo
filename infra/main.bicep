targetScope = 'resourceGroup'

@description('Azure region for the resource group and all resources.')
param location string = resourceGroup().location

@description('Base name prefix for all Azure resources. Use lowercase letters and numbers.')
param baseName string

@description('App Service plan SKU (e.g., F1, B1, S1).')
param appServiceSkuName string = 'B1'

@description('App Service plan tier (e.g., Free, Basic, Standard).')
param appServiceSkuTier string = 'Basic'

module app 'app.bicep' = {
  name: '${baseName}-app'
  params: {
    baseName: baseName
    location: location
    appServiceSkuName: appServiceSkuName
    appServiceSkuTier: appServiceSkuTier
  }
}

output resourceGroup string = resourceGroup().name
output webAppName string = app.outputs.webAppName
output webAppDefaultHostName string = app.outputs.webAppDefaultHostName
output signalRConnectionString string = app.outputs.signalRConnectionString
