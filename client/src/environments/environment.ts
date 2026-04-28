// Local development. The API runs on http://localhost:5080 by default
// (see api/QuantamAnalytics.Api/Properties/launchSettings.json).
//
// Auth0 values default to empty so unauthenticated dev runs are fine. To
// test the login flow locally, paste the same values you set in GitHub
// repo variables (AUTH0_DOMAIN / AUTH0_AUDIENCE / AUTH0_CLIENT_ID).
export const environment = {
  production: false,
  apiBase: 'http://localhost:5080',
  auth0: {
    domain: '',
    clientId: '',
    audience: '',
  },
};
