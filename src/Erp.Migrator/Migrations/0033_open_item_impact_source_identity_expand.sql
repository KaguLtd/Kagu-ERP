ALTER TABLE party.open_item_impact_event
    ADD COLUMN source_type varchar(120),
    ADD COLUMN source_version bigint,
    ADD COLUMN source_posting_purpose varchar(120);

-- Existing impact rows remain available for controlled classification. Every new
-- impact must carry the exact identity used by Accounting posting evidence.
ALTER TABLE party.open_item_impact_event
    ADD CONSTRAINT ck_open_item_impact_source_identity_required
    CHECK
    (
        source_type IS NOT NULL
        AND source_type = btrim(source_type)
        AND source_type <> ''
        AND source_version IS NOT NULL
        AND source_version > 0
        AND source_posting_purpose IS NOT NULL
        AND source_posting_purpose = btrim(source_posting_purpose)
        AND source_posting_purpose <> ''
    ) NOT VALID;

COMMENT ON COLUMN party.open_item_impact_event.source_type IS
    'Canonical source type for the impact economic event. NULL is reserved only for pre-0033 rows.';
COMMENT ON COLUMN party.open_item_impact_event.source_version IS
    'Positive immutable source version used for exact Accounting posting evidence.';
COMMENT ON COLUMN party.open_item_impact_event.source_posting_purpose IS
    'Canonical Accounting posting purpose for this impact event.';
