import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright configuration for Quantam Analytics E2E.
 *
 * Targets the deployed dev environment by default. Override with
 * E2E_BASE_URL to point at staging, prod, or a local dev server.
 *
 *   E2E_BASE_URL=https://my-feature-pr.azurestaticapps.net npx playwright test
 *
 * Tagging:
 *   @smoke       — must always pass; runs on every PR
 *   @authenticated — needs storageState.json; skipped in CI until secret is set
 *   @flaky        — quarantined; runs but doesn't gate
 */

const baseURL =
  process.env.E2E_BASE_URL ??
  'https://ambitious-dune-099500b0f.7.azurestaticapps.net';

// If the auth-state secret is provided in CI, decode it once and write it
// to disk so the authenticated specs can pick it up via storageState.
// In local dev, devs run `npm run auth:save` (TODO) to generate this.
const storageStatePath = process.env.E2E_STORAGE_STATE_PATH ?? 'storage-state.json';

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 2 : undefined,
  reporter: [
    ['list'],
    ['html', { open: 'never' }],
    ['junit', { outputFile: 'test-results/junit.xml' }],
  ],
  use: {
    baseURL,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },
  projects: [
    {
      name: 'public',
      testMatch: /.*\.(public|smoke)\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'authenticated',
      testMatch: /.*\.auth\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        storageState: storageStatePath,
      },
      // Auth specs only run when a storageState file is actually present.
      // CI sets E2E_HAS_STORAGE_STATE=1 after writing the secret to disk.
      ...(process.env.E2E_HAS_STORAGE_STATE
        ? {}
        : { testIgnore: /.*\.auth\.spec\.ts/ }),
    },
  ],
});
