/*
  Application Insights — workspace-based mode (the only mode allowed for new
  resources). Sends telemetry into the shared Log Analytics workspace.
  Sampling and adaptive rate limits are at default; first 5 GB/month is free
  via the workspace quota.
*/

param name string
param location string
param tags object

@description('ARM resource ID of the Log Analytics workspace this AI instance writes to.')
param logAnalyticsWorkspaceId string

resource appi 'Microsoft.Insights/components@2020-02-02' = {
  name: name
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    DisableLocalAuth: false
  }
}

output id string = appi.id
output name string = appi.name

@secure()
output connectionString string = appi.properties.ConnectionString
