/*
  Container Apps Environment — the shared compute boundary that one or more
  Container Apps live in. Uses the consumption-only workload profile (no
  always-on dedicated nodes), which is what enables scale-to-zero.

  Logs are forwarded into the same Log Analytics workspace as App Insights.
*/

param name string
param location string
param tags object

@description('Log Analytics workspace customer ID (GUID).')
param logAnalyticsWorkspaceCustomerId string

@description('Log Analytics workspace primary shared key.')
@secure()
param logAnalyticsWorkspaceSharedKey string

resource cae 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspaceCustomerId
        sharedKey: logAnalyticsWorkspaceSharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
    zoneRedundant: false
  }
}

output id string = cae.id
output name string = cae.name
output defaultDomain string = cae.properties.defaultDomain
