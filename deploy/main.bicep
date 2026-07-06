// ---------------------------------------------------------------------------
// LFZ Plot Management — Azure deployment template
// Topology: App Service plan (Linux) hosting LFZ.Api + LFZ.Web,
//           Azure SQL Database, Log Analytics + Application Insights.
// Deploy:   az deployment group create -g <rg> -f main.bicep -p main.parameters.json
// ---------------------------------------------------------------------------

@description('Base name applied to all resources, e.g. lfz-prod')
param baseName string = 'lfz'

@description('Azure region')
param location string = resourceGroup().location

@description('SQL administrator login')
param sqlAdminLogin string

@secure()
@description('SQL administrator password')
param sqlAdminPassword string

@secure()
@description('JWT signing key (32+ characters)')
param jwtKey string

@description('App Service plan SKU')
@allowed(['B1', 'B2', 'S1', 'P0v3', 'P1v3'])
param appServiceSku string = 'B1'

var sqlServerName = '${baseName}-sql'
var sqlDbName = 'LfzPlots'
var planName = '${baseName}-plan'
var apiAppName = '${baseName}-api'
var webAppName = '${baseName}-web'

// ---------------------------------------------------------------------------
// Observability
// ---------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${baseName}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${baseName}-ai'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ---------------------------------------------------------------------------
// SQL
// ---------------------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource sqlDb 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDbName
  location: location
  sku: {
    name: 'S0'
    tier: 'Standard'
  }
}

// Allow Azure services (App Service) to reach the SQL server
resource sqlFirewallAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDbName};User Id=${sqlAdminLogin};Password=${sqlAdminPassword};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

// ---------------------------------------------------------------------------
// Compute
// ---------------------------------------------------------------------------
resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  kind: 'linux'
  sku: {
    name: appServiceSku
  }
  properties: {
    reserved: true
  }
}

var sharedAppSettings = [
  { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
  { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
  { name: 'ConnectionStrings__DefaultConnection', value: sqlConnectionString }
]

resource apiApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: appServiceSku != 'B1'
      minTlsVersion: '1.2'
      appSettings: concat(sharedAppSettings, [
        { name: 'Jwt__Issuer', value: 'LFZ.Api' }
        { name: 'Jwt__Audience', value: 'LFZ.Clients' }
        { name: 'Jwt__Key', value: jwtKey }
        { name: 'Jwt__ExpiryMinutes', value: '60' }
      ])
    }
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: appServiceSku != 'B1'
      minTlsVersion: '1.2'
      webSocketsEnabled: true // Blazor Server circuits
      appSettings: sharedAppSettings
    }
  }
}

output apiUrl string = 'https://${apiApp.properties.defaultHostName}'
output webUrl string = 'https://${webApp.properties.defaultHostName}'
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
