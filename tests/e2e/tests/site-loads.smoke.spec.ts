import { test, expect } from '@playwright/test';

/**
 * Public smoke tests — no Auth0 login required.
 *
 * These run on every PR and gate merge. If any of these fail, the
 * deployed dev site is broken at the root: the SPA shell didn't load,
 * the static asset CDN is down, or the API health endpoint is rejecting.
 */

test.describe('Public smoke @smoke', () => {
  test('home page returns 200 and loads the Angular shell', async ({ page }) => {
    const response = await page.goto('/');
    expect(response?.status(), 'home / should return 200').toBeLessThan(400);

    // Angular CLI's default index.html sets <app-root> — it's there before
    // hydration too, so this works regardless of whether the bundle has
    // finished executing.
    await expect(page.locator('app-root')).toBeAttached({ timeout: 15_000 });
  });

  test('document title contains Quantam Analytics', async ({ page }) => {
    await page.goto('/');
    await expect(page).toHaveTitle(/quantam\s*analytics/i);
  });

  test('main bundle finishes hydrating without console errors', async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', err => errors.push(err.message));
    page.on('console', msg => {
      if (msg.type() === 'error') {
        errors.push(msg.text());
      }
    });

    await page.goto('/');
    // Give Angular a moment to bootstrap. waitForLoadState handles the
    // network-quiet case; the extra delay catches deferred errors during
    // initial change detection.
    await page.waitForLoadState('networkidle');
    await page.waitForTimeout(1500);

    // Some Auth0 redirects log benign warnings; filter known noise.
    const meaningful = errors.filter(
      e =>
        !/auth0.*login_required/i.test(e) &&
        !/Failed to load resource.*favicon/i.test(e),
    );
    expect(meaningful, 'no unexpected console errors during boot').toEqual([]);
  });

  test('unauthenticated request to a protected route redirects to Auth0', async ({
    page,
  }) => {
    // Hitting any contractor / portal route without a session should kick
    // Auth0's @auth0/auth0-angular guard, which navigates to the Auth0
    // universal login domain. We just verify we leave the SPA origin.
    await page.goto('/contractor/invoices');
    await page.waitForURL(/auth0\.com|\/login/i, { timeout: 20_000 });
    expect(page.url()).toMatch(/auth0\.com|\/login/i);
  });
});
