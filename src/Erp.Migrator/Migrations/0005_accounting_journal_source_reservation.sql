CREATE SCHEMA IF NOT EXISTS accounting AUTHORIZATION kagu_erp_schema_owner;
ALTER SCHEMA accounting OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA accounting FROM PUBLIC;
GRANT USAGE ON SCHEMA accounting TO kagu_erp_app;

CREATE UNIQUE INDEX uq_company_scope_identity
    ON org.company (tenant_id, id);

CREATE TABLE accounting.journal_source_reservation
(
    reservation_id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    source_type varchar(120) NOT NULL,
    source_event_id uuid NOT NULL,
    posting_purpose varchar(120) NOT NULL,
    journal_draft_hash char(64) NOT NULL,
    reserved_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    reserved_by uuid NOT NULL,
    CONSTRAINT fk_journal_source_reservation_company
        FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT uq_journal_source_reservation_identity
        UNIQUE (tenant_id, company_id, source_type, source_event_id, posting_purpose),
    CONSTRAINT ck_journal_source_reservation_source_type
        CHECK (source_type = btrim(source_type) AND source_type <> ''),
    CONSTRAINT ck_journal_source_reservation_posting_purpose
        CHECK (posting_purpose = btrim(posting_purpose) AND posting_purpose <> ''),
    CONSTRAINT ck_journal_source_reservation_hash
        CHECK (journal_draft_hash ~ '^[0-9a-f]{64}$')
);

CREATE INDEX ix_journal_source_reservation_scope_time
    ON accounting.journal_source_reservation (tenant_id, company_id, reserved_at, reservation_id);

ALTER TABLE accounting.journal_source_reservation OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE accounting.journal_source_reservation FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE accounting.journal_source_reservation TO kagu_erp_app;

ALTER TABLE accounting.journal_source_reservation ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.journal_source_reservation FORCE ROW LEVEL SECURITY;
CREATE POLICY journal_source_reservation_scope_policy ON accounting.journal_source_reservation
    FOR ALL
    TO kagu_erp_app
    USING
    (
        tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY
        (
            coalesce(
                nullif(current_setting('app.company_ids', true), '')::uuid[],
                ARRAY[]::uuid[]
            )
        )
    )
    WITH CHECK
    (
        tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY
        (
            coalesce(
                nullif(current_setting('app.company_ids', true), '')::uuid[],
                ARRAY[]::uuid[]
            )
        )
    );
CREATE POLICY journal_source_reservation_schema_owner_policy
    ON accounting.journal_source_reservation
    FOR ALL
    TO kagu_erp_schema_owner
    USING (true)
    WITH CHECK (true);

ALTER DEFAULT PRIVILEGES FOR ROLE kagu_erp_schema_owner IN SCHEMA accounting
    REVOKE ALL ON TABLES FROM PUBLIC;
