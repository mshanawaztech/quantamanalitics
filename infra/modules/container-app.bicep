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
var allSecrets = hasPostgres ? concat(baseSecrets, postgresSecret) : baseSecrets

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

var allEnv = concat(
  baseEnv,
  hasPostgres ? postgresEnv : [],
  hasCors ? corsEnv : [],
  hasAuth0 ? auth0Env : []
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
