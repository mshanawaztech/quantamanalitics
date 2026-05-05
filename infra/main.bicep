/*
  Quantam Analytics — dev environment.

  Scope: resourceGroup. Create the RG with `az group create` first; this
  template fills it. See infra/README.md for full deploy steps.

  Cost target: $0–10/month. Choices that protect that:
    - Container App scales to 0 when idle (consumption profile)
    - SWA Free SKU
    - Key Vault Standard (per-op pennies, dev usage = ~$0)
    - Application Insights piggybacks on a single Log Analytics workspace
    - Postgres lives in Neon free tier — NOT in Azure (no PaaS DB cost)
*/

targetScope = 'resourceGroup'

// ── Parameters ──────────────────────────────────────────────────────────────

@description('Short stack name. Used as a prefix for every resource.')
@minLength(2)
@maxLength(12)
param stack string = 'qa'

@description('Environment slug — dev | stg | prd. Suffix on every resource.')
@allowed([ 'dev', 'stg', 'prd' ])
param environmentName string = 'dev'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Container image for the API. Defaults to a hello-world; PR-05 overrides this from the GHA workflow with the real image tag.')
param apiImage string = 'mcr.microsoft.com/azuredocs/containerapps-helloworld:latest'

@description('Object IDs (AAD) of users/principals that should have Key Vault Secrets Officer access in dev. Empty = nobody (you add yourself manually after first deploy).')
param keyVaultAdminPrincipalIds array = []

@description('Postgres connection string in Npgsql keyword form (Host=...;Database=...;Username=...;Password=...;SslMode=Require). Empty default lets the bare-bicep deploy still succeed when Postgres is not yet wired up; the GHA workflow always passes the real value from secrets.POSTGRES_CONNECTION_STRING.')
@secure()
param postgresConnectionString string = ''

@description('Auth0 tenant domain (e.g. quantamanalitics-dev.us.auth0.com). NOT a secret. Empty = API runs unauthenticated; the workflow passes the real value from vars.AUTH0_DOMAIN.')
param auth0Domain string = ''

@description('Auth0 API audience identifier (e.g. https://api.quantamanalitics.com). NOT a secret. Empty = API runs unauthenticated; the workflow passes the real value from vars.AUTH0_AUDIENCE.')
param auth0Audience string = ''

@description('Cloudflare R2 account ID. Empty = resume uploads stay disabled in the API.')
@secure()
param r2AccountId string = ''

@description('Cloudflare R2 access key ID. Empty = resume uploads stay disabled in the API.')
@secure()
param r2AccessKeyId string = ''

@description('Cloudflare R2 secret access key. Empty = resume uploads stay disabled in the API.')
@secure()
param r2SecretAccessKey string = ''

@description('Cloudflare R2 bucket name for candidate resume uploads. Empty = resume uploads stay disabled in the API.')
@secure()
param r2Bucket string = ''

// ── Naming ─────────────────────────────────────────────────────────────────
// CAF-style abbreviations. Suffix unique per env so prd resources don't
// collide with dev. Globally-unique names (KV, SWA) get a uniqueString suffix.

var prefix = '${stack}-${environmentName}'
var uniq = uniqueString(resourceGroup().id, stack, environmentName)

var names = {
  logAnalytics: 'log-${prefix}'
  appInsights: 'appi-${prefix}'
  keyVault: 'kv-${prefix}-${take(uniq, 6)}'
  containerAppsEnv: 'cae-${prefix}'
  containerApp: 'ca-${prefix}-api'
  staticWebApp: 'swa-${prefix}-${take(uniq, 6)}'
}

var tags = {
  stack: stack
  environment: environmentName
  managedBy: 'bicep'
  project: 'quantamanalitics'
}

// ── Modules ─────────────────────────────────────────────────────────────────

module logs 'modules/log-analytics.bicep' = {
  name: 'logs'
  params: {
    name: names.logAnalytics
    location: location
    tags: tags
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsights'
  params: {
    name: names.appInsights
    location: location
    tags: tags
    logAnalyticsWorkspaceId: logs.outputs.workspaceId
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault'
  params: {
    name: names.keyVault
    location: location
    tags: tags
    adminPrincipalIds: keyVaultAdminPrincipalIds
  }
}

module containerAppsEnv 'modules/container-apps-env.bicep' = {
  name: 'containerAppsEnv'
  params: {
    name: names.containerAppsEnv
    location: location
    tags: tags
    logAnalyticsWorkspaceCustomerId: logs.outputs.customerId
    logAnalyticsWorkspaceSharedKey: logs.outputs.sharedKey
  }
}

module staticWebApp 'modules/static-web-app.bicep' = {
  name: 'staticWebApp'
  params: {
    name: names.staticWebApp
    // SWA Free SKU is only available in a few regions. East US 2 -> use eastus2.
    // If you change `location`, this may need a separate SWA region param.
    location: location
    tags: tags
  }
}

// Container App goes last so it can reference the SWA hostname for CORS.
// Bicep figures out the dependency order from the param refs — no need for
// explicit `dependsOn`.
module containerApp 'modules/container-app.bicep' = {
  name: 'containerApp'
  params: {
    name: names.containerApp
    location: location
    tags: tags
    environmentId: containerAppsEnv.outputs.id
    image: apiImage
    appInsightsConnectionString: appInsights.outputs.connectionString
    postgresConnectionString: postgresConnectionString
    // Comma-separated CORS allow-list. Includes the SWA hostname so the
    // Angular app deployed there can call the API. Add custom domains here
    // (or via a new param) once they're issued.
    corsAllowedOrigins: 'https://${staticWebApp.outputs.defaultHostname}'
    auth0Domain: auth0Domain
    auth0Audience: auth0Audience
    r2AccountId: r2AccountId
    r2AccessKeyId: r2AccessKeyId
    r2SecretAccessKey: r2SecretAccessKey
    r2Bucket: r2Bucket
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────
// Surfaced so GitHub Actions in PR-05 can read them without re-deriving names.
// Secrets are NOT outputted — deployment outputs end up in plaintext history.
// The Container App already has the AI connection string injected as a secret
// inside its own template (see modules/container-app.bicep).

output resourceGroupName string = resourceGroup().name
output keyVaultName string = keyVault.outputs.name
output keyVaultUri string = keyVault.outputs.uri
output containerAppsEnvironmentId string = containerAppsEnv.outputs.id
output containerAppName string = containerApp.outputs.name
output containerAppUrl string = containerApp.outputs.fqdn
output staticWebAppName string = staticWebApp.outputs.name
output staticWebAppDefaultHostname string = staticWebApp.outputs.defaultHostname
