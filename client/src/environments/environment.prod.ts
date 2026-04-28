// Production build. The deploy workflow rewrites this file at build time
// (see .github/workflows/deploy-dev.yml — "Build for dev" step) with:
//   - apiBase set to the live Container App FQDN
//   - auth0.{domain,clientId,audience} set from the AUTH0_* GitHub variables
// Anything you put here will be overwritten on the next deploy.
export const environment = {
  production: true,
  apiBase: '',
  auth0: {
    domain: '',
    clientId: '',
    audience: '',
  },
};
