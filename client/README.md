# Quantam Analytics — Client

Angular 21 SPA. Hosts all four user portals (recruiter / client / candidate / contractor) behind route guards once auth lands in PR-06.

## Local development

```bash
npm install
npm start              # ng serve on http://localhost:4200
```

The dev server expects the API on `http://localhost:5080` (see `src/environments/environment.ts`). Start it from the repo root:

```bash
dotnet run --project ../api/QuantamAnalytics.Api
```

The landing page probes `/health` so you can verify front-end ↔ back-end connectivity at a glance.

## Other commands

```bash
npm run build          # production bundle into dist/
npm test               # vitest unit tests
npm run lint           # angular-eslint (when configured)
```

## Folder layout

```
src/
├── app/
│   ├── core/           singletons (services, interceptors, guards)
│   │   └── health/     example: API health probe
│   ├── app.config.ts
│   ├── app.routes.ts
│   ├── app.ts
│   └── ...
├── environments/
│   ├── environment.ts        local dev (API on :5080)
│   └── environment.prod.ts   built into prod bundle
├── index.html
└── styles.scss
```

Feature modules (`features/marketing`, `features/jobs`, `features/recruiter`, etc.) will land in their respective phase-1 PRs.
