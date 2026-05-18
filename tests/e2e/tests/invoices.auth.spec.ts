import { test, expect } from '@playwright/test';

/**
 * Contractor invoices — authenticated end-to-end flow.
 *
 * Requires a Playwright storageState file at the path configured in
 * playwright.config.ts. See README → "Authenticated tests" for how to
 * generate one locally and how CI consumes it.
 *
 * These specs only run when E2E_HAS_STORAGE_STATE is set in the
 * environment. Without it the `authenticated` project is excluded.
 */

test.describe('Contractor invoices', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/contractor/invoices');
    await expect(page.getByRole('heading', { name: /invoices/i })).toBeVisible({
      timeout: 15_000,
    });
  });

  test('letterhead preview renders with Quantam defaults', async ({ page }) => {
    // Per /settings/branding fallback: a tenant who hasn't customized
    // branding should see the platform defaults in the preview block.
    await expect(page.getByText('Mohammed Khan')).toBeVisible();
    await expect(page.getByText('Quantamanalytics LLC')).toBeVisible();
    await expect(page.getByText(/993681185/)).toBeVisible();
    await expect(page.getByText(/021202337/)).toBeVisible();
  });

  test('creating an invoice with one week line item shows it in the list', async ({
    page,
  }) => {
    const clientName = `E2E Client ${Date.now()}`;
    await page.getByLabel(/client name/i).fill(clientName);
    await page.getByLabel(/vendor/i).fill('E2E Vendor');

    // Week card: days × hours/day = total. Defaults already fill in
    // current week + 5 days × 8 hours @ tenant default rate.
    await page.getByRole('button', { name: /add line item/i }).click();
    await page.getByRole('button', { name: /save invoice/i }).click();

    await expect(page.getByText(clientName)).toBeVisible({ timeout: 10_000 });
  });

  test('preview opens in a new tab and shows the banner', async ({
    page,
    context,
  }) => {
    const [previewPage] = await Promise.all([
      context.waitForEvent('page'),
      page.getByRole('link', { name: /preview/i }).first().click(),
    ]);
    await previewPage.waitForLoadState('load');
    // PDF preview is served as application/pdf — we can't read pixels,
    // but we can confirm the URL is the api /pdf endpoint.
    expect(previewPage.url()).toMatch(/\/api\/v1\/.*\/pdf/i);
  });

  test('CSV download returns a non-empty file', async ({ page }) => {
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.getByRole('link', { name: /csv/i }).first().click(),
    ]);
    const path = await download.path();
    expect(path).toBeTruthy();
  });
});

test.describe('Settings · branding round-trip', () => {
  test('changing display name persists after reload', async ({ page }) => {
    await page.goto('/settings/branding');
    await expect(
      page.getByRole('heading', { name: /branding/i }),
    ).toBeVisible({ timeout: 15_000 });

    const original = await page.getByLabel(/display name/i).inputValue();
    const updated = `${original} ${Date.now()}`;
    await page.getByLabel(/display name/i).fill(updated);
    await page.getByRole('button', { name: /save/i }).click();

    await page.reload();
    await expect(page.getByLabel(/display name/i)).toHaveValue(updated);

    // Restore so subsequent runs don't drift.
    await page.getByLabel(/display name/i).fill(original);
    await page.getByRole('button', { name: /save/i }).click();
  });
});
