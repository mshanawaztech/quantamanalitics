/*
  Key Vault for application secrets — Auth0 client secret, Resend API key,
  Cloudflare R2 credentials, etc. Container App reads via secret references
  using its system-assigned managed identity (wired in PR-05/PR-06).

  Uses RBAC, not access policies (Microsoft's recommended path since 2023).
  Soft-delete is on by default; purge protection is OFF for dev so we can
  recreate the vault freely. Turn it ON in stg/prd via the param.
*/

param name string
param location string
param tags object

@description('AAD principal IDs that get Key Vault Secrets Officer in dev. Usually just your own object ID.')
param adminPrincipalIds array = []

@description('Block hard-deletes of secrets. Off for dev (lets us nuke and recreate); on for stg/prd.')
param enablePurgeProtection bool = false

resource kv 'Microsoft.KeyVault/vaults@2024-04-01-preview' = {
  name: name
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: enablePurgeProtection ? true : null
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: 'Allow'
    }
  }
}

// Key Vault Secrets Officer (read + write secrets, manage versions).
// Built-in role: b86a8fe4-44ce-4948-aee5-eccb2c155cd7
var secretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

resource adminAssignments 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for principalId in adminPrincipalIds: {
  name: guid(kv.id, principalId, secretsOfficerRoleId)
  scope: kv
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', secretsOfficerRoleId)
    principalId: principalId
    principalType: 'User'
  }
}]

output id string = kv.id
output name string = kv.name
output uri string = kv.properties.vaultUri
