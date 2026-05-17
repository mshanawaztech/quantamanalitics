-- ─────────────────────────────────────────────────────────────────────────
-- TenantBrandingSchema — data backfill.
--
-- Paste into the auto-generated migration's Up() method via
-- migrationBuilder.Sql(@"...");, AFTER the CreateTable("tenant_branding")
-- call and AFTER any indexes are created. Drops a default empty row for
-- every existing tenant so the SPA's GET endpoint never has to upsert.
-- ─────────────────────────────────────────────────────────────────────────

INSERT INTO tenant_branding (
    id,
    tenant_id,
    primary_color_hex,
    accent_color_hex,
    default_currency,
    default_payment_terms_days,
    created_at_utc,
    updated_at_utc
)
SELECT
    gen_random_uuid()                       AS id,
    t.id                                    AS tenant_id,
    '#1a2d5a'                               AS primary_color_hex,
    '#e6c9a8'                               AS accent_color_hex,
    'USD'                                   AS default_currency,
    14                                      AS default_payment_terms_days,
    NOW() AT TIME ZONE 'UTC'                AS created_at_utc,
    NOW() AT TIME ZONE 'UTC'                AS updated_at_utc
FROM tenants t
WHERE NOT EXISTS (
    SELECT 1 FROM tenant_branding b WHERE b.tenant_id = t.id
);
