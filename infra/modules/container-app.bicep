/*
  The API Container App. Scales 0 → 1 on traffic, idle = $0.

  System-assigned managed identity is enabled so the app can pull secrets
  from Key Vault (wired up in PR-05) without storing credentials anywhere.

  Health probes hit /health (liveness, no DB) and /ready (readiness, DB hit).
  /ready is the gate the load balancer uses to decide if a replica gets
  traffic — so a Postgres outage takes the app out of rotation immediately.
*/

param name string
param location string
param tags object

@description('ARM resource ID of the Container Apps environment.')
param environmentId string

@description('Container image to run. Override per-deploy from CI.')
param image string

@description('Application Insights connection string. Forwarded to the app as APPLICATIONINSIGHTS_CONNECTION_STRING.')
@secure()
param appInsightsConnectionString string

@description('Postgres connection string in Npgsql keyword form. When non-empty, surfaced to the app as ConnectionStrings__Postgres via a Container App secret.')
@secure()
param postgresConnectionString string = ''

@description('Comma-separated list of CORS allow-list origins. Surfaced to the app as Cors__AllowedOrigins (= IConfiguration["Cors:AllowedOrigins"]). Empty = app falls back to its appsettings default of localhost:4200.')
param corsAllowedOrigins string = ''

@description('Auth0 tenant domain (e.g. quantamanalitics-dev.us.auth0.com). NOT a secret — public OIDC discovery hostname. Empty = app runs unauthenticated.')
param auth0Domain string = ''

@description('Auth0 API audience identifier (e.g. https://api.quantamanalitics.com). NOT a secret — public token claim. Empty = app runs unauthenticated.')
param auth0Audience string = ''

@description('Cloudflare R2 account ID. Empty = resume uploads stay disabled.')
@secure()
param r2AccountId string = ''

@description('Cloudflare R2 access key ID. Empty = resume uploads stay disabled.')
@secure()
param r2AccessKeyId string = ''

@description('Cloudflare R2 secret access key. Empty = resume uploads stay disabled.')
@secure()
param r2SecretAccessKey string = ''

@description('Cloudflare R2 bucket name. Empty = resume uploads stay disabled.')
@secure()
param r2Bucket string = ''

@description('Public-facing base URL of the SPA, e.g. https://ambitious-dune-099500b0f.7.azurestaticapps.net. Used by anonymous public-facing endpoints (the Indeed job feed, future feeds) when constructing per-job URLs that crawlers / external systems will follow back into the app. Empty = the API falls back to its own request scheme+host, which is wrong for crawled feeds in prod.')
param publicWebBaseUrl string = ''

@description('When true, the API seeds demo tenants, jobs, and applications at startup.')
param demoDataSeedOnStartup bool = false

@description('Port the container listens on. Must match ASPNETCORE_HTTP_PORTS.')
param targetPort int = 8080

@description('Minimum replicas. 0 = scale to zero on idle (lowest cost).')
@minValue(0)
@maxValue(25)
param minReplicas int = 0

@description('Maximum replicas. Keep low in dev to cap cost on a runaway loop.')
@minValue(1)
@maxValue(25)
param maxReplicas int = 1

// Conditionally include the postgres secret + env var only when a value
// was passed — keeps the bare-bicep deploy from blowing up if Postgres
// isn't wired yet.
var hasPostgres = !empty(postgresConnectionString)

var baseSecrets = [
  {
    name: 'appinsights-connection-string'
    value: appInsightsConnectionString
  }
]
var postgresSecret = [
  {
    name: 'postgres-connection-string'
    value: postgresConnectionString
  }
]

var baseEnv = [
  {
    name: 'ASPNETCORE_ENVIRONMENT'
    value: 'Development'
  }
  {
    name: 'ASPNETCORE_HTTP_PORTS'
    value: string(targetPort)
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    secretRef: 'appinsights-connection-string'
  }
]
var postgresEnv = [
  {
    // Double-underscore = ASP.NET Core configuration's nested-key separator,
    // so this maps to ConnectionStrings:Postgres in IConfiguration, which is
    // exactly what AddInfrastructure() reads via GetConnectionString("Postgres").
    name: 'ConnectionStrings__Postgres'
    secretRef: 'postgres-connection-string'
  }
]

// CORS allow-list — same double-underscore trick maps to Cors:AllowedOrigins.
// Only emitted when the param is non-empty so local-style deploys (with the
// app's appsettings default) keep working unchanged.
var hasCors = !empty(corsAllowedOrigins)
var corsEnv = [
  {
    name: 'Cors__AllowedOrigins'
    value: corsAllowedOrigins
  }
]

// Auth0 OIDC config. Only emit when both halves are present — half-set
// would crash AddPlatformAuth's validation. Empty default keeps pre-Auth0
// deployments running unauthenticated (a warning logs at startup).
var hasAuth0 = !empty(auth0Domain) && !empty(auth0Audience)
var auth0Env = [
  {
    name: 'Auth0__Domain'
    value: auth0Domain
  }
  {
    name: 'Auth0__Audience'
    value: auth0Audience
  }
]

var hasR2 = !empty(r2AccountId) && !empty(r2AccessKeyId) && !empty(r2SecretAccessKey) && !empty(r2Bucket)
var r2Secrets = [
  {
    name: 'r2-account-id'
    value: r2AccountId
  }
  {
    name: 'r2-access-key-id'
    value: r2AccessKeyId
  }
  {
    name: 'r2-secret-access-key'
    value: r2SecretAccessKey
  }
  {
    name: 'r2-bucket'
    value: r2Bucket
  }
]
var r2Env = [
  {
    name: 'Storage__R2__AccountId'
    secretRef: 'r2-account-id'
  }
  {
    name: 'Storage__R2__AccessKeyId'
    secretRef: 'r2-access-key-id'
  }
  {
    name: 'Storage__R2__SecretAccessKey'
    secretRef: 'r2-secret-access-key'
  }
  {
    name: 'Storage__R2__Bucket'
    secretRef: 'r2-bucket'
  }
]

var demoDataEnv = [
  {
    name: 'DemoData__SeedOnStartup'
    value: string(demoDataSeedOnStartup)
  }
]

// Public web base URL (SWA hostname) for anonymous feeds. Same double-
// underscore mapping convention — surfaces as PublicWeb:BaseUrl in
// IConfiguration, which JobFeedEndpoint reads at request time. Only
// emitted when the param is non-empty so local-style deploys keep
// falling back to the request scheme+host.
var hasPublicWebBaseUrl = !empty(publicWebBaseUrl)
var publicWebEnv = [
  {
    name: 'PublicWeb__BaseUrl'
    value: publicWebBaseUrl
  }
]

var allEnv = concat(
  baseEnv,
  hasPostgres ? postgresEnv : [],
  hasCors ? corsEnv : [],
  hasAuth0 ? auth0Env : [],
  hasR2 ? r2Env : [],
  hasPublicWebBaseUrl ? publicWebEnv : [],
  demoDataEnv
)
var allSecrets = concat(
  hasPostgres ? concat(baseSecrets, postgresSecret) : baseSecrets,
  hasR2 ? r2Secrets : []
)

resource ca 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    environmentId: environmentId
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: targetPort
        transport: 'auto'
        allowInsecure: false
        traffic: [
          {
            latestRevision: true
            weight: 100
          }
        ]
      }
      secrets: allSecrets
    }
    template: {
      containers: [
        {
          name: 'api'
          image: image
          resources: {
            // Minimum billable size on consumption profile.
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: allEnv
          probes: [
            // Liveness: process is up. /health returns immediately with no
            // dependencies, so the default 1s timeout is fine.
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: targetPort
              }
              initialDelaySeconds: 10
              periodSeconds: 30
              timeoutSeconds: 5
              failureThreshold: 3
            }
            // Readiness: /ready opens a Postgres connection via EF Core. On a
            // cold-start of Neon's pooler that round-trip can take 5–15s, so
            // give the probe room to wait. failureThreshold: 6 + periodSeconds:
            // 15 = ~90s before giving up — enough for Neon to wake from sleep
            // without blowing past the GHA workflow's smoke-test budget.
            {
              type: 'Readiness'
              httpGet: {
                path: '/ready'
                port: targetPort
              }
              initialDelaySeconds: 15
              periodSeconds: 15
              timeoutSeconds: 10
              failureThreshold: 6
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '50'
              }
            }
          }
        ]
      }
    }
  }
}

output id string = ca.id
output name string = ca.name
output fqdn string = ca.properties.configuration.ingress.fqdn
output principalId string = ca.identity.principalId
