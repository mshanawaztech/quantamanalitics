-- ─────────────────────────────────────────────────────────────────────────
-- InvoiceLineItemsV4Schema — backfill for new week-based columns.
--
-- Paste INSIDE the auto-generated migration's Up() method via
-- migrationBuilder.Sql(@"...");, AFTER the AddColumn calls and BEFORE any
-- AlterColumn calls that flip the new columns to NOT NULL.
--
-- Pre-v4 rows have no week range and no DaysWorked / HoursPerDay split.
-- We seed:
--   week_start_utc = NULL  (already nullable, no change needed)
--   week_end_utc   = NULL
--   days_worked    = 1     (placeholder so Hours = HoursPerDay maps cleanly)
--   hours_per_day  = hours (preserves the total — Hours stays the same)
--   notes          = NULL
-- ─────────────────────────────────────────────────────────────────────────

UPDATE invoice_line_items
SET
    days_worked   = COALESCE(days_worked, 1),
    hours_per_day = COALESCE(hours_per_day, hours);
