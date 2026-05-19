#!/usr/bin/env -S npx tsx
//
// save-auth-state.ts — interactive helper that captures an authenticated
// browser session and writes it to storage-state.json for the Playwright
// auth specs.
//
// Use this once per Auth0 test user (or whenever a stored session expires).
// The resulting file is gitignored; for CI, base64-encode it and stuff the
// value into the E2E_AUTH0_STORAGE_STATE_B64 GitHub Actions secret —
// .github/workflows/ci.yml's `e2e` job decodes it back into the same path.
//
// Usage:
//   cd tests/e2e
//   npm install                  # one-time
//   npm run auth:save             # opens Chromium for you to log in
//   # — log in via Auth0, wait for the dashboard, then close the window
//
// Override the target URL:
//   E2E_BASE_URL=https://my-preview.azurestaticapps.net npm run auth:save
//
// The script waits for either the dashboard URL OR for you to close the
// window manually — whichever comes first. It then writes storage state to
// `tests/e2e/storage-state.json`.

import { chromium } from '@playwright/test';
import * as path from 'node:path';
import * as fs from 'node:fs/promises';

const BASE_URL =
  process.env.E2E_BASE_URL ??
  'https://ambitious-dune-099500b0f.7.azurestaticapps.net';

const OUT_PATH = path.resolve(
  process.cwd(),
  process.env.E2E_STORAGE_STATE_PATH ?? 'storage-state.json',
);

// Routes that mean "you're logged in" — first match wins. Add more if the
// app's post-login landing pad changes per role.
const POST_LOGIN_PATHS = ['/dashboard', '/contractor', '/recruiter', '/client'];

async function main(): Promise<void> {
  console.log(`Launching Chromium against ${BASE_URL}`);
  console.log(
    `When you've finished logging in, the script will save storage state to:\n  ${OUT_PATH}`,
  );

  const browser = await chromium.launch({ headless: false });
  const context = await browser.newContext();
  const page = await context.newPage();

  // Race two ways the user finishes: either Auth0 routes them to a known
  // logged-in URL, or they close the window. Whichever resolves first wins.
  const loggedInBy = await Promise.race([
    page
      .goto(BASE_URL)
      .then(() =>
        page.waitForURL(
          url => POST_LOGIN_PATHS.some(p => url.pathname.startsWith(p)),
          { timeout: 5 * 60 * 1000 }, // five minutes to complete Auth0 login
        ),
      )
      .then(() => 'navigation' as const),
    new Promise<'closed'>(resolve => {
      page.on('close', () => resolve('closed'));
      browser.on('disconnected', () => resolve('closed'));
    }),
  ]).catch(err => {
    console.error('Login wait failed:', err);
    return 'failed' as const;
  });

  if (loggedInBy === 'failed') {
    console.error(
      `\n⚠  Did not detect a known post-login URL within the timeout.\n` +
        `   If you DID log in, the dashboard URL pattern may have changed;\n` +
        `   add it to POST_LOGIN_PATHS in this script.`,
    );
    await browser.close().catch(() => {});
    process.exit(1);
  }

  // Capture storage state before tearing down the context. Works even if the
  // user closed the window — Playwright's context retains state until close.
  try {
    await context.storageState({ path: OUT_PATH });
    console.log(`\n✓ Saved storage state to ${OUT_PATH}`);
    console.log(
      `\nNext: encode it for CI:\n  base64 -w0 storage-state.json | pbcopy   # macOS\n  base64 -w0 storage-state.json | xclip -selection clipboard   # Linux\n` +
        `Then paste into the GitHub repo secret E2E_AUTH0_STORAGE_STATE_B64.`,
    );
  } catch (err) {
    console.error('Could not write storage state:', err);
    process.exit(1);
  } finally {
    await browser.close().catch(() => {});
  }

  // Sanity: confirm the file is non-empty (~200 bytes at minimum).
  const stat = await fs.stat(OUT_PATH);
  if (stat.size < 100) {
    console.warn(
      `\n⚠  ${OUT_PATH} is suspiciously small (${stat.size} bytes). The session may not have been authenticated.`,
    );
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
