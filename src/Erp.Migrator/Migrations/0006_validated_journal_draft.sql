CREATE UNIQUE INDEX uq_journal_source_reservation_scope_id
    ON accounting.journal_source_reservation (tenant_id, company_id, reservation_id);

CREATE TABLE accounting.validated_journal_draft
(
    journal_draft_id uuid PRIMARY KEY,
    reservation_id uuid NOT NULL UNIQUE,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    posting_rule_version_id uuid NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL,
    functional_currency char(3) NOT NULL,
    draft_hash char(64) NOT NULL,
    total_debit numeric(20,4) NOT NULL,
    total_credit numeric(20,4) NOT NULL,
    line_count integer NOT NULL,
    persisted_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    persisted_by uuid NOT NULL,
    CONSTRAINT fk_validated_journal_draft_reservation
        FOREIGN KEY (tenant_id, company_id, reservation_id)
        REFERENCES accounting.journal_source_reservation (tenant_id, company_id, reservation_id),
    CONSTRAINT uq_validated_journal_draft_scope_id
        UNIQUE (tenant_id, company_id, journal_draft_id),
    CONSTRAINT ck_validated_journal_draft_currency
        CHECK (functional_currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_validated_journal_draft_hash
        CHECK (draft_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_validated_journal_draft_balanced
        CHECK (total_debit > 0 AND total_debit = total_credit),
    CONSTRAINT ck_validated_journal_draft_line_count
        CHECK (line_count >= 2)
);

CREATE TABLE accounting.validated_journal_line
(
    journal_draft_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    line_number integer NOT NULL,
    account_id uuid NOT NULL,
    source_line_id uuid NULL,
    debit numeric(20,4) NOT NULL,
    credit numeric(20,4) NOT NULL,
    dimensions jsonb NOT NULL,
    currency_snapshot jsonb NULL,
    CONSTRAINT pk_validated_journal_line PRIMARY KEY (journal_draft_id, line_number),
    CONSTRAINT fk_validated_journal_line_draft
        FOREIGN KEY (tenant_id, company_id, journal_draft_id)
        REFERENCES accounting.validated_journal_draft (tenant_id, company_id, journal_draft_id),
    CONSTRAINT ck_validated_journal_line_number CHECK (line_number > 0),
    CONSTRAINT ck_validated_journal_line_amount
        CHECK (debit >= 0 AND credit >= 0 AND ((debit > 0) <> (credit > 0))),
    CONSTRAINT ck_validated_journal_line_dimensions_array
        CHECK (jsonb_typeof(dimensions) = 'array'),
    CONSTRAINT ck_validated_journal_line_currency_object
        CHECK (currency_snapshot IS NULL OR jsonb_typeof(currency_snapshot) = 'object')
);

CREATE INDEX ix_validated_journal_draft_scope_effective
    ON accounting.validated_journal_draft (tenant_id, company_id, effective_date, journal_draft_id);
CREATE INDEX ix_validated_journal_line_scope_account
    ON accounting.validated_journal_line (tenant_id, company_id, account_id, journal_draft_id);

ALTER TABLE accounting.validated_journal_draft OWNER TO kagu_erp_schema_owner;
ALTER TABLE accounting.validated_journal_line OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE accounting.validated_journal_draft, accounting.validated_journal_line FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE accounting.validated_journal_draft, accounting.validated_journal_line TO kagu_erp_app;

ALTER TABLE accounting.validated_journal_draft ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.validated_journal_draft FORCE ROW LEVEL SECURITY;
ALTER TABLE accounting.validated_journal_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.validated_journal_line FORCE ROW LEVEL SECURITY;

CREATE POLICY validated_journal_draft_scope_policy ON accounting.validated_journal_draft
    FOR ALL TO kagu_erp_app
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY validated_journal_draft_owner_policy ON accounting.validated_journal_draft
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);

CREATE POLICY validated_journal_line_scope_policy ON accounting.validated_journal_line
    FOR ALL TO kagu_erp_app
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY validated_journal_line_owner_policy ON accounting.validated_journal_line
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
