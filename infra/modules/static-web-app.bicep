/*
  Static Web App for the Angular client. Free SKU = 100 GB egress/month,
  unlimited preview environments per PR, free SSL on custom domains.

  Bicep only stands the resource up. The actual deploy (build + upload) is
  done by the Azure/static-web-apps-deploy GitHub Action in PR-05, which
  uses the deployment token surfaced as an output below.
*/

param name string
param location string
param tags object

@description('Free | Standard. Free is enough for everything we need in dev.')
@allowed([ 'Free', 'Standard' ])
param sku string = 'Free'

resource swa 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    // We deploy via GitHub Actions explicitly (not the SWA-managed pipeline
    // generation), so leave repository fields empty. The "build" runs in our
    // own workflow and uploads via the deployment token.
    allowConfigFileUpdates: true
    enterpriseGradeCdnStatus: 'Disabled'
  }
}

output id string = swa.id
output name string = swa.name
output defaultHostname string = swa.properties.defaultHostname
