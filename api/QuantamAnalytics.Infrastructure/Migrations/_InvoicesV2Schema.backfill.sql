-- ─────────────────────────────────────────────────────────────────────────
-- InvoicesV2Schema — data backfill for existing rows.
--
-- Paste these statements INSIDE the auto-generated migration's Up() method
-- via `migrationBuilder.Sql(@"...")`, between the AddColumn calls and the
-- AlterColumn calls that flip the new columns to NOT NULL.
--
-- The auto-generated migration handles the DDL (new tables, columns,
-- indexes). The SQL here handles the data: it gives every pre-existing
-- invoice a deterministic invoice number, populates the new scalar fields
-- from existing data, primes the per-tenant invoice_number_sequences
-- table so future mints don't collide, and writes one InvoiceLineItem per
-- legacy invoice so the v2 reader sees a non-empty collection.
-- ─────────────────────────────────────────────────────────────────────────

-- 1. Assign deterministic invoice numbers (INV-{year}-{seq}), grouped by
--    tenant + year of created_at_utc, ordered by created_at_utc.
WITH ranked AS (
    SELECT
        id,
        tenant_id,
        EXTRACT(YEAR FROM created_at_utc)::int AS yr,
        ROW_NUMBER() OVER (
            PARTITION BY tenant_id, EXTRACT(YEAR FROM created_at_utc)
            ORDER BY created_at_utc, id
        ) AS seq
    FROM invoices
    WHERE invoice_number IS NULL OR invoice_number = ''
)
UPDATE invoices i
SET invoice_number = 'INV-' || ranked.yr || '-' || LPAD(ranked.seq::text, 4, '0')
FROM ranked
WHERE i.id = ranked.id;

-- 2. Backfill the scalar v2 fields from the legacy single-line shape.
UPDATE invoices
SET
    client_name = COALESCE(NULLIF(client_name, ''), ''),
    issue_date_utc = COALESCE(issue_date_utc, period_start_utc),
    due_date_utc = COALESCE(due_date_utc, period_end_utc + INTERVAL '14 days'),
    subtotal = COALESCE(subtotal, amount),
    tax_rate = COALESCE(tax_rate, 0),
    tax_amount = COALESCE(tax_amount, 0);

-- 3. Prime invoice_number_sequences so the next mint per (tenant, year)
--    skips past the backfilled numbers.
INSERT INTO invoice_number_sequences (tenant_id, year, next_value, updated_at_utc)
SELECT
    tenant_id,
    EXTRACT(YEAR FROM created_at_utc)::int AS year,
    MAX(
        CAST(SUBSTRING(invoice_number FROM '\d+$') AS INTEGER)
    ) + 1 AS next_value,
    NOW() AT TIME ZONE 'UTC' AS updated_at_utc
FROM invoices
WHERE invoice_number ~ '^INV-\d{4}-\d+$'
GROUP BY tenant_id, EXTRACT(YEAR FROM created_at_utc)
ON CONFLICT (tenant_id, year) DO UPDATE
    SET next_value = GREATEST(invoice_number_sequences.next_value, EXCLUDED.next_value),
        updated_at_utc = EXCLUDED.updated_at_utc;

-- 4. Synthesize one line item per legacy invoice. Rate = amount/hours when
--    hours > 0, otherwise rate = 0 and amount stays on the invoice header.
INSERT INTO invoice_line_items (id, invoice_id, description, hours, rate, amount, sort_order)
SELECT
    gen_random_uuid() AS id,
    i.id AS invoice_id,
    'Hours billed' AS description,
    i.hours,
    CASE WHEN i.hours > 0 THEN ROUND(i.amount / i.hours, 4) ELSE 0 END AS rate,
    i.amount,
    0 AS sort_order
FROM invoices i
WHERE NOT EXISTS (
    SELECT 1 FROM invoice_line_items li WHERE li.invoice_id = i.id
);
