targetScope = 'resourceGroup'

@description('Base name prefix for all Azure resources. Use lowercase letters and numbers.')
param baseName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('App Service plan SKU (e.g., F1, B1, S1).')
param appServiceSkuName string = 'B1'

@description('App Service plan tier (e.g., Free, Basic, Standard).')
param appServiceSkuTier string = 'Basic'

var planName = '${baseName}-plan'
var webAppName = '${baseName}-web'
var signalRName = '${baseName}-signalr'

resource plan 'Microsoft.Web/serverfarms@2022-09-01' = {
  name: planName
  location: location
  sku: {
    name: appServiceSkuName
    tier: appServiceSkuTier
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource signalR 'Microsoft.SignalRService/signalR@2023-02-01' = {
  name: signalRName
  location: location
  sku: {
    name: 'Free_F1'
    tier: 'Free'
  }
  properties: {
    tls: {
      clientCertEnabled: false
    }
    features: [
      {
        flag: 'ServiceMode'
        value: 'Default'
      }
      {
        flag: 'EnableConnectivityLogs'
        value: 'True'
      }
      {
        flag: 'EnableMessagingLogs'
        value: 'False'
      }
    ]
  }
}

var signalRPrimaryConnection = listKeys(signalR.id, '2023-02-01').primaryConnectionString

resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    httpsOnly: true
    serverFarmId: plan.id
    siteConfig: {
      linuxFxVersion: 'DOTNET|7.0'
      appSettings: [
        {
          name: 'Azure__SignalR__ConnectionString'
          value: signalRPrimaryConnection
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output webAppDefaultHostName string = webApp.properties.defaultHostName
output signalRConnectionString string = signalRPrimaryConnection
