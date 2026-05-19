# E2E tests (Playwright)

Smoke + regression suite for the deployed Quantam Analytics SPA.

## Running locally

```bash
cd tests/e2e
npm install
npm run install:browsers   # one-time
npm test                    # full suite
npm run test:smoke          # public smoke only — no auth needed
npm run test:headed         # see the browser
npm run test:ui             # Playwright UI runner
```

By default tests target the dev deployment
(`https://ambitious-dune-099500b0f.7.azurestaticapps.net`). Override:

```bash
E2E_BASE_URL=http://localhost:4200 npm test
```

## Tags

| Tag             | Meaning                                                                |
| --------------- | ---------------------------------------------------------------------- |
| `@smoke`        | Public, must-pass. Runs on every PR. Failing = site is broken at root. |
| `@authenticated` | Requires a logged-in storage state. Skipped unless one is provided.   |
| `@flaky`        | Quarantined. Runs but doesn't gate.                                    |

## Authenticated tests

The contractor invoices and `/settings/branding` specs need a real
Auth0 session because the app guards those routes behind
`@auth0/auth0-angular`.

### Generate `storage-state.json` locally

```bash
npm run auth:save
# Chromium opens. Complete Auth0 login with your test user.
# Script detects the post-login redirect and saves storage-state.json
# automatically. Close the window if it gets stuck.
```

Then:

```bash
E2E_HAS_STORAGE_STATE=1 npm test
```

### Wiring CI to run authenticated specs

1. Generate `storage-state.json` locally (above).
2. Base64-encode it: `base64 -w0 storage-state.json > storage-state.b64`
3. Add the contents of that file as a GitHub repository secret named
   `E2E_AUTH0_STORAGE_STATE_B64`.
4. The `e2e` job in `.github/workflows/ci.yml` will detect it and run
   the authenticated project on every PR.

Test users should be created in a dedicated Auth0 tenant if possible
so leaked storage states only ever grant access to throwaway data.

## File layout

```
tests/e2e/
├── package.json
├── playwright.config.ts
├── tsconfig.json
├── scripts/
│   └── save-auth-state.ts        # `npm run auth:save` — interactive Auth0 capture
└── tests/
    ├── site-loads.smoke.spec.ts   # public smoke
    └── invoices.auth.spec.ts      # contractor invoices + branding (authed)
```

## Adding a new spec

- Public, no login: name it `*.smoke.spec.ts` and tag with `@smoke`.
- Logged-in: name it `*.auth.spec.ts`. It will be excluded from the
  `public` project automatically.
