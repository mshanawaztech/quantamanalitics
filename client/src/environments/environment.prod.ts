// Production build. With the API hosted on the same custom domain
// (api.quantamanalitics.com vs www.quantamanalitics.com), apiBase is empty
// and requests use a cross-origin absolute path baked in via the deploy step.
// We'll wire this properly in PR-04 (infra) and PR-05 (CI/CD).
export const environment = {
  production: true,
  apiBase: '',
};
