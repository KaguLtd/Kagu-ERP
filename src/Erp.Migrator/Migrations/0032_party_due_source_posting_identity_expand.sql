ALTER TABLE party.due_schedule
    ADD COLUMN source_effective_date date,
    ADD COLUMN source_posting_purpose varchar(120);

-- NOT VALID keeps pre-0032 schedules readable for controlled classification while
-- rejecting every new schedule that cannot be joined to exact Accounting posting evidence.
ALTER TABLE party.due_schedule
    ADD CONSTRAINT ck_due_schedule_source_posting_identity_required
    CHECK
    (
        source_effective_date IS NOT NULL
        AND source_posting_purpose IS NOT NULL
        AND source_posting_purpose = btrim(source_posting_purpose)
        AND source_posting_purpose <> ''
    ) NOT VALID;

COMMENT ON COLUMN party.due_schedule.source_effective_date IS
    'Legal/economic source date used independently from recorded_at for as-of reporting and exact posting evidence.';
COMMENT ON COLUMN party.due_schedule.source_posting_purpose IS
    'Canonical Accounting posting purpose. NULL is reserved only for pre-0032 rows awaiting controlled classification.';
