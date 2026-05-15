# Auth hardening playbook — Phase 8 / Story 63

Companion doc to `docs/auth.md`. This is the list of Auth0-side and
platform-side controls that make sign-in trustworthy enough for an
enterprise security review. Most of the work happens in the Auth0
dashboard; this file is the single place that names what to do, why,
and how it ties back to the in-product surfaces we ship.

## In-product surfaces (code lives in this repo)

| Surface | Where |
| --- | --- |
| Sign-out-everywhere request | `POST /api/v1/me/security/sessions/revoke` → records a `security.session_revoked` notification + audit log row for the calling user. |
| Suspicious-login landing pad | `POST /api/v1/me/security/suspicious-login` → called from the Auth0 post-login action below. Writes a `security.suspicious_login` notification so the user sees a banner next time they sign in. |
| Notification dropdown (consumer) | `client/src/app/core/notifications/notifications-bell.component.ts` — both notification kinds surface here. |
| Audit-log trail (compliance) | `audit_log_entries` table — both events land via the existing `AuditLogSaveChangesInterceptor`. |

The platform deliberately does **not** call Auth0's Management API at
revoke time. Revoking is two steps:

1. The user clicks "Sign out of all devices" — we record their intent
   above so the audit trail is honest.
2. A follow-up job (left as a TODO in the endpoint comment) hits
   `https://{domain}/api/v2/users/{id}/multifactor` and
   `revoke_grants` via the Management API to invalidate live refresh
   tokens. That step needs an M2M client id + secret (sensitive) so it
   sits behind a Hangfire background job, not a synchronous endpoint
   call.

## Auth0-side controls (do these in the dashboard)

### MFA

Dashboard → **Security → Multi-factor Auth**.

- Enable **OTP** (Google Authenticator / Authy etc.) and **WebAuthn**.
- Set the policy to **Always** for the production tenant. For the dev
  tenant keep it on **Never** so the team can iterate without juggling
  a phone.
- Under "Define policies", scope MFA to roles that touch sensitive
  data: `Recruiter`, `Client`, `PlatformAdmin`. Candidates and
  Contractors are exempt by default.

### Breach-password detection

Dashboard → **Security → Attack Protection → Breached password
detection**.

- Set **Detection mode** to **Block** for the production tenant.
- Enable email notification to the affected user. The platform's own
  notification surface is best-effort — Auth0's email is the
  authoritative channel because it works even after we've forced
  the password reset.

### Brute-force protection

Dashboard → **Security → Attack Protection → Brute-force protection**.

- Leave the default (block after 10 failed sign-ins per source IP).
- Set the notification recipient to your security alias.

### Refresh-token rotation

Dashboard → **Applications → {SPA} → Settings → Refresh Token Rotation**.

- Enable **Rotation**.
- Set **Reuse Interval** to 30 seconds. Anything longer and a leaked
  refresh token has a multi-minute window of free use.
- Set **Absolute Lifetime** to 7 days.
- Set **Inactivity Lifetime** to 24 hours.

The Angular SPA's Auth0 SDK respects these settings without code
changes — but if rotation is enabled and the SDK is older than
`@auth0/auth0-angular@2.2`, refresh calls will silently fail. Pin
the version in `client/package.json`.

### Post-login action (suspicious-login → notification)

Dashboard → **Actions → Flows → Login → Add Action → Custom**.

```js
exports.onExecutePostLogin = async (event, api) => {
  const newDevice = event.user.user_metadata?.last_country &&
    event.user.user_metadata.last_country !== event.request.geoip.country_code;

  if (newDevice) {
    // Best-effort POST to our suspicious-login landing pad. We do not
    // block the login on failure — the user still gets in, just without
    // an in-app heads-up.
    await fetch(`${event.secrets.API_BASE_URL}/api/v1/me/security/suspicious-login`, {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${event.accessToken?.toString() ?? ''}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        location: `${event.request.geoip.cityName}, ${event.request.geoip.countryName}`,
        atUtc: new Date().toISOString(),
      }),
    }).catch(() => undefined);
  }

  api.user.setUserMetadata('last_country', event.request.geoip.country_code);
};
```

Set the `API_BASE_URL` secret in the Action's secrets to the prod API
host (e.g. `https://api.quantamanalitics.com`).

### Session control

Dashboard → **Tenant Settings → Advanced → Login Session Management**.

- **Inactivity timeout**: 3 days.
- **Require log in after**: 7 days.
- These match the refresh-token absolute lifetime above.

## Production cutover checklist

- [ ] MFA enforced for `Recruiter`, `Client`, `PlatformAdmin` in the
      prod tenant.
- [ ] Breach-password detection set to **Block**.
- [ ] Refresh-token rotation enabled with 30s reuse window.
- [ ] Suspicious-login post-login Action enabled and pointing at the
      prod API host.
- [ ] `API_BASE_URL` Action secret matches the deployed API.
- [ ] Management API M2M client + secret created and stored in
      `Auth0:ManagementClientId` / `Auth0:ManagementClientSecret`
      app settings — required by the revoke-grants follow-up job.

## Open follow-ups

- **Revoke-grants follow-up job** — wire a Hangfire recurring task that
  drains pending revocations through the Management API. Tracked under
  `qa001-revoke-grants-followup` (post Phase 8).
- **Step-up auth on dangerous endpoints** — `acr_values=mfa` challenge
  on `DELETE /api/v1/recruiter/...` paths. Adds friction; do it when
  the first customer asks for it.
- **Session-listing UI** — let users see a list of active devices and
  revoke one at a time. Needs an Auth0 Management API roundtrip per
  session; deferred until customer demand.
