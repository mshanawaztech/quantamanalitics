# Auth0 setup

The API validates JWTs issued by an Auth0 tenant. This doc walks through the one-time tenant configuration. After it's done, every deploy that has the right env vars will require a valid bearer token on every endpoint that doesn't `.AllowAnonymous()`.

## What's wired up

| Where | What |
| --- | --- |
| `api/QuantamAnalytics.Api/Auth/AuthExtensions.cs` | JWT bearer registration against Auth0 + role-based authorization policies. **No-op** when `Auth0:Domain` / `Auth0:Audience` aren't set — API runs unauthenticated with a startup warning. |
| `api/QuantamAnalytics.Api/Endpoints/MeEndpoint.cs` | `GET /me` — first authenticated endpoint. Returns the JWT's `sub`, `email`, `name`, roles, and tenant_id. |
| `api/QuantamAnalytics.Domain/Common/Roles.cs` | Canonical role names (`PlatformAdmin`, `Recruiter`, `Candidate`) and the custom-claim namespace constants. |
| `infra/main.bicep` | `auth0Domain` + `auth0Audience` parameters → injected into the Container App as env vars. |
| `.github/workflows/deploy-dev.yml` | Reads `vars.AUTH0_DOMAIN` / `vars.AUTH0_AUDIENCE` (GitHub *variables*, not secrets) and passes them through to bicep. |

Public values (domain, audience, client ID) live in **GitHub repository variables** because they're discoverable from any signed JWT — they're not secrets.

## One-time tenant setup

Do these in your browser at **[manage.auth0.com](https://manage.auth0.com)**.

### 1. Create the tenant

- Sign up free at [auth0.com](https://auth0.com)
- Tenant domain: `quantamanalitics-dev` (region added automatically → e.g. `quantamanalitics-dev.us.auth0.com`)
- Region: US (closest to our Azure East US 2)
- Account type: Personal

Note your full tenant domain — you'll need it everywhere as `Auth0:Domain`.

### 2. Create the API resource

- Left sidebar: **Applications → APIs → + Create API**
- **Name:** `Quantam Analytics API`
- **Identifier:** `https://api.quantamanalitics.com` (just a unique string — doesn't have to resolve)
- **Signing Algorithm:** RS256

The Identifier becomes your `Auth0:Audience`.

### 3. Create the SPA application

- Left sidebar: **Applications → Applications → + Create Application**
- **Name:** `Quantam Analytics — Web Client`
- **Type:** Single Page Web Applications

In its Settings tab:

- **Allowed Callback URLs:** `http://localhost:4200, https://<your-swa-hostname>.azurestaticapps.net`
- **Allowed Logout URLs:** same
- **Allowed Web Origins:** same
- Save

The **Client ID** value is needed by the Angular app (PR-06.5 / PR-08).

### 4. Define the roles

**User Management → Roles → + Create Role**

Create three roles, names exact (they're string constants in `Roles.cs`):

- `PlatformAdmin`
- `Recruiter`
- `Candidate`

Adding more roles later is fine — just keep the existing three intact.

### 5. Add the custom-claims Action

Auth0 doesn't put roles in the JWT by default. We use a Post-Login Action to inject them under our claim namespace.

- **Actions → Library → Build Custom**
- **Name:** `Add roles and tenant_id to tokens`
- **Trigger:** Login / Post Login
- **Runtime:** Node 18

Replace the editor contents with:

```javascript
exports.onExecutePostLogin = async (event, api) => {
  const namespace = 'https://quantamanalitics.com/';

  // Roles assigned in Auth0 → User Management → Users → <user> → Roles
  const roles = (event.authorization?.roles ?? []);
  if (roles.length > 0) {
    api.idToken.setCustomClaim(`${namespace}roles`, roles);
    api.accessToken.setCustomClaim(`${namespace}roles`, roles);
  }

  // tenant_id lives in app_metadata (Auth0 → Users → <user> → app_metadata)
  const tenantId = event.user.app_metadata?.tenant_id;
  if (tenantId) {
    api.idToken.setCustomClaim(`${namespace}tenant_id`, tenantId);
    api.accessToken.setCustomClaim(`${namespace}tenant_id`, tenantId);
  }
};
```

Click **Deploy** (top right).

Then **Actions → Triggers → post-login** → drag `Add roles and tenant_id to tokens` into the flow → **Apply**.

The namespace `https://quantamanalitics.com/` is hardcoded in `Roles.cs`. Don't change it without updating both places.

## One-time GitHub setup

After the tenant is set up, populate **repository variables** (not secrets) at **Settings → Secrets and variables → Actions → Variables tab**.

| Name | Value |
| --- | --- |
| `AUTH0_DOMAIN` | `quantamanalitics-dev.us.auth0.com` |
| `AUTH0_AUDIENCE` | `https://api.quantamanalitics.com` |
| `AUTH0_CLIENT_ID` | the SPA application's Client ID |

The deploy workflow reads `vars.AUTH0_DOMAIN` + `vars.AUTH0_AUDIENCE` and passes them through to bicep, which injects them as `Auth0__Domain` / `Auth0__Audience` env vars on the Container App.

`AUTH0_CLIENT_ID` is for the Angular app — picked up later when PR-06.5 / PR-08 wire up `@auth0/auth0-angular`.

## Local dev

For local dev (running the API on your laptop), set the same two values via `dotnet user-secrets`:

```
cd api/QuantamAnalytics.Api
dotnet user-secrets set "Auth0:Domain" "quantamanalitics-dev.us.auth0.com"
dotnet user-secrets set "Auth0:Audience" "https://api.quantamanalitics.com"
```

Restart the API. Without these, you'll see a startup log line:

```
warn: Program[0]
      Auth0 is not configured (Auth0:Domain / Auth0:Audience missing).
      Running with authentication DISABLED — every endpoint is public.
```

That's fine for hacking offline; just don't expose that build to the internet.

## Creating your first user

1. **User Management → Users → + Create User**
2. **Email:** your email
3. **Password:** anything (you'll log in with it via Auth0's hosted page later)
4. **Connection:** `Username-Password-Authentication`
5. Save

To assign a role: open the user → **Roles** tab → **Assign Roles** → pick e.g. `PlatformAdmin`.

To assign a tenant_id: open the user → **app_metadata** field on the Details tab → set `{ "tenant_id": "<the-tenant-uuid-from-the-tenants-table>" }` → Save.

Once roles and metadata are set, log in via your SPA. The Action injects them into the JWT, and the API reads them via `User.IsInRole("Recruiter")` / the `https://quantamanalitics.com/tenant_id` claim.

## Verifying it works

Once the deploy is green and tokens are flowing, hit `/me` from any logged-in browser session:

```
curl -i -H "Authorization: Bearer <token>" \
  https://ca-qa-dev-api.<region>.azurecontainerapps.io/me
```

Expected:

```json
{
  "sub": "auth0|<long-id>",
  "email": "you@example.com",
  "name": "Your Name",
  "roles": ["PlatformAdmin"],
  "tenantId": "<uuid>"
}
```

Without a token: `401 Unauthorized`.
With an invalid/expired token: `401 Unauthorized`.

## Production hardening (Phase 5)

Before going live with real users:

- Rotate the tenant from `*-dev` to `quantamanalitics.us.auth0.com` (Auth0 lets you create a new prod tenant; configs migrate via the Management API)
- Lock the SPA's Allowed Origins to your real domain (no `localhost`)
- Turn on MFA, brute-force protection, and breached-password detection in the Auth0 dashboard
- Move the Auth0 management API key into Key Vault and grant Container App managed identity read access (only needed if we automate user provisioning)
