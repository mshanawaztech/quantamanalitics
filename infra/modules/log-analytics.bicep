/*
  Log Analytics workspace. One per environment, shared by Application Insights
  and the Container Apps environment so everything ends up in one queryable
  pane and one billing bucket. PerGB2018 is the standard pay-per-GB SKU; first
  5 GB/month is free.
*/

@description('Workspace name. Globally unique within the subscription is not required, but should be unique within the RG.')
param name string

param location string
param tags object

@description('Days to retain ingested logs. 30 is the free-tier max — anything higher costs ~$0.10/GB/month.')
@minValue(30)
@maxValue(730)
param retentionDays int = 30

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

output workspaceId string = workspace.id
output customerId string = workspace.properties.customerId

@secure()
output sharedKey string = workspace.listKeys().primarySharedKey
