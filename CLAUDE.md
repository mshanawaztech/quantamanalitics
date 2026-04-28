# CLAUDE.md — Project Memory

This file is read by Claude on every session in this repo. Treat the rules below as non-negotiable.

## Project

**Quantam Analytics** — a multi-tenant staffing SaaS platform. Built first for one staffing firm, designed from day 1 to be sold as SaaS.

Domain: `quantamanalitics.com`
Owner: Shahnawaz Mohammed (`mshanawaztech@gmail.com`)
GitHub: `mshanawaz114/quantamanalitics`

Full roadmap: [`plan/plan.md`](./plan/plan.md)
Current phase deliverables: [`plan/phase-1-deliverables.md`](./plan/phase-1-deliverables.md)

## Stack (locked — do not change without an ADR)

- Backend: ASP.NET Core 10 LTS, EF Core 10
- Database: PostgreSQL (dev: Neon free tier; prod: Azure Database for PostgreSQL Flexible Server)
- Background jobs: Hangfire (in-process)
- Frontend: Angular (LTS), single SPA with route-based portals (recruiter / client / candidate / contractor)
- Auth: Auth0 free tier (or Azure B2C — picked in PR-06)
- File storage: Cloudflare R2
- Hosting: Azure Container Apps (API), Azure Static Web Apps (web)
- Email: Resend (transactional), Cloudflare Email Routing (mailboxes)
- CI/CD: GitHub Actions
- Infra-as-code: Bicep
- Observability: Application Insights

## Branch & PR rules — non-negotiable

1. **Never push to `main`.** Ever. `main` is protected and only accepts squash-merged PRs.
2. **Branch naming:** `qa001-<short-scope>` (e.g., `qa001-bootstrap`, `qa001-postgres-efcore`). The `qa001` prefix is permanent.
3. **One PR = one deliverable.** If a PR exceeds ~600 changed lines, split it.
4. **PR template required** — see `.github/pull_request_template.md`. Fill in every section.
5. **Squash-merge only.** Keeps `main` history linear and readable.
6. **Commit message style:** `<type>: <short summary>` where type is one of `feat | fix | chore | docs | refactor | test | ci`. Body explains *why*, not *what*.
7. **Update CLAUDE.md and README.md at every milestone PR** (currently at the end of each Phase). Stale docs are worse than no docs.

## Folder structure (forecasted — populated as PRs land)

```
quantamanalitics/
├── api/                       ← .NET 10 solution (PR-02)
│   ├── QuantamAnalytics.Api/
│   ├── QuantamAnalytics.Domain/
│   ├── QuantamAnalytics.Infrastructure/
│   └── QuantamAnalytics.Tests/
├── client/                    ← Angular workspace (PR-02)
├── infra/                     ← Bicep (PR-04)
├── plan/                      ← roadmap docs
│   ├── plan.md
│   └── phase-1-deliverables.md
├── docs/                      ← ADRs, runbooks (added as needed)
│   └── adr/
├── .github/
│   ├── workflows/
│   └── pull_request_template.md
├── .claude/                   ← Claude project settings + skills
│   ├── settings.json
│   └── skills/
├── CLAUDE.md                  ← this file
├── README.md
├── CONTRIBUTING.md
├── LICENSE
├── .gitignore
└── .editorconfig
```

## Things to NEVER do

- Never commit secrets (API keys, connection strings, tokens). Use Azure Key Vault. `.env` files are gitignored — keep them that way.
- Never push directly to `main`. Always PR.
- Never disable EF Core global query filters in production code. Tenant isolation depends on them.
- Never store sensitive PII (SSN, DOB) without encryption-at-rest column types.
- Never accept a tenant_id from the client — always derive from the authenticated user's JWT.
- Never bump a major dependency version without an ADR explaining why.
- Never skip the PR template.

## Things to ALWAYS do

- Always run `dotnet format` and `npm run lint` before committing
- Always include the `qa001-` prefix on branch names
- Always reference the PR number in commit messages once a PR is open (`feat(jobs): list endpoint paging (#9)`)
- Always update `plan/phase-1-deliverables.md` status column when a PR merges

## Current state

- **Active phase:** Phase 1 — MVP
- **Active deliverable:** PR-06.5 · Angular Auth0 SDK on `qa001-auth-spa` (open PR)
- **Last merged:** PR-06 · Auth foundation server-side (JWT bearer + /me)
- **Next deliverable:** PR-07 · Multi-tenant plumbing

### What PR-06.5 added (read these before extending)

- **`@auth0/auth0-angular` ^2.2.3** in `client/package.json`. Wraps the Auth0 SPA SDK with Angular DI + observables + an HTTP interceptor.
- **`client/src/app/app.config.ts`** — `provideAuth0(...)` registers the SDK with config from `environment.auth0`. `httpInterceptor.allowedList` scopes the Bearer token to API calls only (never SWA static assets, never third-party). `cacheLocation: 'localstorage'` + `useRefreshTokens: true` survives page refresh.
- **`client/src/environments/environment.ts` + `environment.prod.ts`** — new `auth0: { domain, clientId, audience }` block. Empty defaults make the SDK install but `loginWithRedirect()` becomes a no-op (UI shows a "not configured" message). The deploy workflow rewrites `environment.prod.ts` with the live values from GitHub vars `AUTH0_DOMAIN` / `AUTH0_CLIENT_ID` / `AUTH0_AUDIENCE`.
- **`client/src/app/core/auth/auth.service.ts`** — facade over the Auth0 SDK. Exposes `isConfigured`, `isAuthenticated`, `isLoading`, `user`, `email` as Angular signals. `loginWithRedirect()` and `logout()` are no-ops when not configured. **Always import this**, never the Auth0 SDK directly.
- **`client/src/app/core/auth/me.service.ts`** — fetches `GET /me` automatically when `isAuthenticated` flips true; clears state on logout. Demonstrates the SDK's HTTP interceptor working — no manual token handling at the call site.
- **`client/src/app/app.html` + `app.scss`** — landing page now has an "Account" card alongside "API health". Login button → Auth0 hosted page → redirect back. While authenticated, shows `sub`, `name`, `email`, `roles[]`, `tenantId` from `/me`. Pattern to copy for protected views in PR-10/11.
- **`.github/workflows/deploy-dev.yml`** — Build for dev step now writes the Auth0 block into `environment.prod.ts` from `vars.AUTH0_*`.

### Required GitHub repository variables (NOT secrets)

Same three from PR-06; PR-06.5 needs `AUTH0_CLIENT_ID` too.

| Name | Used by |
| --- | --- |
| `AUTH0_DOMAIN` | API (server-side validation) + SPA (login redirect) |
| `AUTH0_AUDIENCE` | API (audience claim check) + SPA (access token request) |
| `AUTH0_CLIENT_ID` | SPA only (identifies the SPA application to Auth0) |

### What PR-06 added (read these before extending)

- **`api/QuantamAnalytics.Api/Auth/AuthExtensions.cs`** — JWT bearer + role policies. Returns `bool` indicating whether auth was wired (true if `Auth0:Domain` + `Auth0:Audience` are both set). **Safely no-op when missing** — supports pre-Auth0 deployments.
- **`api/QuantamAnalytics.Api/Auth/AuthorizationPolicies.cs`** — string constants for `RequirePlatformAdmin`, `RequireRecruiter`, `RequireCandidate`. Use these in `.RequireAuthorization(...)` calls instead of stringly-typed role names.
- **`api/QuantamAnalytics.Api/Endpoints/MeEndpoint.cs`** — `GET /me` returns the current JWT's `sub`, `email`, `name`, `roles[]`, `tenantId`. Only routed when auth is wired (otherwise calling `RequireAuthorization()` with no scheme fails at request time).
- **`api/QuantamAnalytics.Domain/Common/Roles.cs`** — three roles: `PlatformAdmin`, `Recruiter`, `Candidate`. Plus the custom-claim namespace `https://quantamanalitics.com/` for `roles` and `tenant_id` claims emitted by Auth0's Post-Login Action.
- **`api/QuantamAnalytics.Api/Program.cs`** — conditionally calls `AddPlatformAuth`, `UseAuthentication`/`UseAuthorization`, `MapMeEndpoint` based on the bool. Logs a startup warning when auth is disabled. `/health` and `/ready` opt out of the global RequireAuthenticatedUser fallback policy via `.AllowAnonymous()`.
- **`infra/main.bicep` + `modules/container-app.bicep`** — new `auth0Domain` and `auth0Audience` params. NOT secret (public OIDC discovery values). Injected as Container App env vars `Auth0__Domain` / `Auth0__Audience` only when both are present.
- **`.github/workflows/deploy-dev.yml`** — reads `vars.AUTH0_DOMAIN` / `vars.AUTH0_AUDIENCE` (GitHub repo *variables*, not secrets) and passes them through. Empty values → API runs unauthenticated with a warning, no breakage.
- **`docs/auth.md`** — full Auth0 tenant setup walkthrough: tenant/API/SPA creation, role definition, Post-Login Action with custom claims, GitHub variables, local user-secrets, first-user creation.

### Required GitHub repository variables (NOT secrets)

These are public OIDC config; using **variables** instead of secrets makes them visible in logs and easier to debug. Set at `Settings → Secrets and variables → Actions → Variables tab`.

| Name | Source |
| --- | --- |
| `AUTH0_DOMAIN` | Auth0 tenant domain (e.g. `quantamanalitics-dev.us.auth0.com`) |
| `AUTH0_AUDIENCE` | API resource Identifier (e.g. `https://api.quantamanalitics.com`) |
| `AUTH0_CLIENT_ID` | SPA application Client ID — used by Angular in PR-06.5 / PR-08 |

### What PR-05 added (read these before extending)

- **`api/Dockerfile`** — multi-stage build (sdk:10.0 → aspnet:10.0). Listens on `:8080` (matches `infra/modules/container-app.bicep` targetPort). Runs as the built-in non-root `app` user. Layer caching is optimized: csproj copy + restore happens before source copy, so source-only changes don't bust the restore layer.
- **`api/.dockerignore`** — excludes `bin/`, `obj/`, the Tests project, IDE state, and any local secrets file. Keep it tight to avoid baking junk into images.
- **`.github/workflows/ci.yml`** — runs on every PR + push to main. Four jobs in parallel: API build/test/format, client lint/build, Bicep syntax check on every `.bicep`, and a no-push Docker build (catches Dockerfile breakage before merge). All four are required status checks on main (set in branch protection — see `docs/cicd.md`).
- **`.github/workflows/deploy-dev.yml`** — runs on push to main + manual dispatch. Three sequential jobs:
  1. `api-image` — builds + pushes `ghcr.io/<owner>/quantamanalitics-api:<sha>` and `:latest` to GHCR.
  2. `deploy-infra` — re-runs the Bicep with `apiImage=ghcr.io/...:<sha>`, then smoke-tests `https://<fqdn>/health` with retries.
  3. `deploy-client` — rewrites `environment.prod.ts` with the live API URL, builds the Angular bundle, ships to SWA.
- **OIDC federation** — `azure/login@v2` uses `AZURE_CLIENT_ID` / `AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID`. No client secret anywhere. Trust is configured per-repo via `az ad app federated-credential create`. Setup steps in `docs/cicd.md`.
- **GHCR for images** — image is public after first-deploy one-click visibility flip in GitHub Packages UI. Container App pulls anonymously. Production hardening (private GHCR + managed identity pull) is a Phase 5 task.
- **Migrations are NOT auto-applied** by deploy-dev. Run them manually with `dotnet ef database update` for now. PR-12 hardening adds a `migrate-dev.yml` workflow.
- **Concurrency rules:** PR runs of CI cancel themselves on new pushes; main runs of CI never cancel. Two deploy-dev runs never overlap (`concurrency: deploy-dev`, no cancel).

### Required GitHub repository secrets

These must exist or both workflows fail. See `docs/cicd.md` for how to obtain each:

| Secret | Source |
| --- | --- |
| `AZURE_CLIENT_ID` | The `appId` of the qa-github-dev AAD app |
| `AZURE_TENANT_ID` | `az account show --query tenantId` |
| `AZURE_SUBSCRIPTION_ID` | `az account show --query id` |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | `az staticwebapp secrets list ... --query properties.apiKey` |

### What PR-04 added (read these before extending)

- `infra/main.bicep` — RG-scoped orchestrator. Subscription / RG creation is done by `az group create` first; this template only fills the RG. Outputs surface every resource name + URL the GHA workflow in PR-05 needs.
- `infra/main.bicepparam` — dev defaults: stack=qa, environment=dev, location=eastus2, placeholder API image. Override `keyVaultAdminPrincipalIds` on the CLI with your own object ID for first deploy.
- `infra/modules/` — one file per resource. Pattern: pinned api-versions, `@description` on every param, `@secure()` on anything secret-like, `output id name fqdn` so other modules (or GHA) can chain off it.
- **Cost discipline baked in:** Container App scales 0→1 (consumption profile), SWA Free SKU, Key Vault Standard, App Insights piggybacks on a single Log Analytics workspace, **Postgres lives in Neon — not in Azure**. Estimated dev run cost: $0–3/month.
- **Resource naming:** CAF abbreviations + `<stack>-<env>` suffix. Globally-unique names (KV, SWA) get a `take(uniqueString(...), 6)` suffix. Don't change the naming scheme — it's referenced from PR-05's GHA workflow.
- **Probes:** Container App liveness hits `/health` (no DB), readiness hits `/ready` (DB hit). A Postgres outage takes the app out of rotation immediately — by design.
- **Image:** until PR-05 lands, the Container App runs `mcr.microsoft.com/azuredocs/containerapps-helloworld`. PR-05 swaps in a real GHCR image via `--parameters apiImage=...` from the workflow.

### Deploying the dev environment

Full instructions live in `infra/README.md`. TL;DR after `az login`:

```
az group create --name rg-qa-dev --location eastus2
MY_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
az deployment group create \
  --resource-group rg-qa-dev \
  --name qa-dev-bootstrap \
  --template-file infra/main.bicep \
  --parameters infra/main.bicepparam \
  --parameters keyVaultAdminPrincipalIds="['$MY_OBJECT_ID']"
```

### What PR-03 added (read these before extending)

- **Domain:**
  - `Domain/Common/IEntity.cs` — base marker. `ITenantScoped : IEntity` is the contract PR-07's global query filter latches onto; every tenant-owned entity must implement it.
  - `Domain/Entities/Tenant.cs` — first real aggregate. `Slug` is the URL-safe natural key (used in subdomains and sign-in), unique and immutable. Constructor self-assigns a UUID v7 via `Guid.CreateVersion7()`.
- **Infrastructure:**
  - `Infrastructure/Data/AppDbContext.cs` — single `DbContext`. Picks up entity configurations by reflection from this assembly.
  - `Infrastructure/Data/Configurations/TenantConfiguration.cs` — pattern to copy for every new entity. `ValueGeneratedNever()` on the PK is mandatory so EF doesn't clobber the v7 Guid set in the ctor.
  - `Infrastructure/DependencyInjection.cs` — single `AddInfrastructure(configuration)` extension. Reads `ConnectionStrings:Postgres`, fails fast in dev with a useful error if missing. Uses `AddDbContextPool` for perf and `UseSnakeCaseNamingConvention()` so generated SQL is idiomatic Postgres.
- **Api:**
  - `Program.cs` — `AddInfrastructure()` wired in. New `GET /ready` endpoint returns 200 only when EF can reach Postgres. `/health` stays pure liveness (no DB).
  - `appsettings.json` has an empty `ConnectionStrings:Postgres` placeholder. Real value lives in `dotnet user-secrets` for dev, env vars in prod. **Never commit a real connection string.**
  - `QuantamAnalytics.Api.csproj` references `Microsoft.EntityFrameworkCore.Design` (PrivateAssets=all) so `dotnet ef` can target it as the startup project.
- **PK strategy:** UUID v7 (`Guid.CreateVersion7()`), generated in entity constructors. Time-ordered → no index hot-spotting. No DB roundtrip to allocate. Safe across multi-tenant + distributed writes. Don't switch to `Identity` columns or v4 Guids without an ADR.
- **Naming:** snake_case at the DB layer (via EFCore.NamingConventions), PascalCase in C#. Don't override per-entity unless there's a reason.
- **Tests:** `AppDbContextModelTests.cs` is the pattern for fast model-validation tests — no real DB, just inspects the EF model. Real DB tests come later via Testcontainers when behavior justifies the spin-up cost.

### Migrations workflow

Migrations live in `api/QuantamAnalytics.Infrastructure/Migrations/` and are checked into source control. Add a new one with:

```
cd api
dotnet ef migrations add <DescriptiveName> \
  --project QuantamAnalytics.Infrastructure \
  --startup-project QuantamAnalytics.Api
```

Apply pending migrations to the connected DB (uses the user-secrets connection string in dev):

```
dotnet ef database update \
  --project QuantamAnalytics.Infrastructure \
  --startup-project QuantamAnalytics.Api
```

Never edit a migration after it's been pushed to a shared branch — add a new one instead.

## Cost discipline

Build phase target: **$0–10/month total run cost.** If any decision pushes spend above this, raise it explicitly in the PR description with the reason. The user is bootstrapping — every recurring dollar matters.

## Working with this project

Before starting any work session in this repo:

1. Read this file (you're doing it).
2. Read `plan/plan.md` if context is light.
3. Check `plan/phase-1-deliverables.md` for the next pending deliverable.
4. Confirm you're on a `qa001-*` branch before making changes.
5. Create the branch from latest `main` if not.
