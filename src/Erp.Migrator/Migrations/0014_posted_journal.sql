ALTER TABLE workflow.approval_completion_snapshot
    ADD CONSTRAINT uq_approval_completion_source_binding
    UNIQUE (tenant_id, company_id, approval_instance_id, subject_type, subject_id, subject_version);

CREATE TABLE accounting.posted_journal
(
    journal_id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    journal_draft_id uuid NOT NULL,
    period_id uuid NOT NULL,
    approval_instance_id uuid NOT NULL,
    source_type varchar(120) NOT NULL,
    source_event_id uuid NOT NULL,
    source_version bigint NOT NULL,
    posting_purpose varchar(120) NOT NULL,
    posting_rule_version_id uuid NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL,
    posted_at timestamptz NOT NULL,
    posted_by uuid NOT NULL,
    functional_currency char(3) NOT NULL,
    draft_hash char(64) NOT NULL,
    total_debit numeric(20,4) NOT NULL,
    total_credit numeric(20,4) NOT NULL,
    line_count integer NOT NULL,
    CONSTRAINT uq_posted_journal_scope_id UNIQUE (tenant_id, company_id, journal_id),
    CONSTRAINT uq_posted_journal_draft UNIQUE (tenant_id, company_id, journal_draft_id),
    CONSTRAINT fk_posted_journal_draft
        FOREIGN KEY (tenant_id, company_id, journal_draft_id)
        REFERENCES accounting.validated_journal_draft (tenant_id, company_id, journal_draft_id),
    CONSTRAINT fk_posted_journal_period
        FOREIGN KEY (tenant_id, company_id, period_id)
        REFERENCES accounting.accounting_period (tenant_id, company_id, period_id),
    CONSTRAINT fk_posted_journal_approval_source
        FOREIGN KEY (tenant_id, company_id, approval_instance_id, source_type, source_event_id, source_version)
        REFERENCES workflow.approval_completion_snapshot
            (tenant_id, company_id, approval_instance_id, subject_type, subject_id, subject_version),
    CONSTRAINT ck_posted_journal_source_version CHECK (source_version > 0),
    CONSTRAINT ck_posted_journal_text CHECK
        (source_type = btrim(source_type) AND source_type <> ''
         AND posting_purpose = btrim(posting_purpose) AND posting_purpose <> ''),
    CONSTRAINT ck_posted_journal_currency CHECK (functional_currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_posted_journal_hash CHECK (draft_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_posted_journal_balanced CHECK (total_debit > 0 AND total_debit = total_credit),
    CONSTRAINT ck_posted_journal_line_count CHECK (line_count >= 2)
);

CREATE TABLE accounting.posted_journal_line
(
    journal_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    line_number integer NOT NULL,
    account_id uuid NOT NULL,
    source_line_id uuid NULL,
    debit numeric(20,4) NOT NULL,
    credit numeric(20,4) NOT NULL,
    dimensions jsonb NOT NULL,
    currency_snapshot jsonb NULL,
    CONSTRAINT pk_posted_journal_line PRIMARY KEY (journal_id, line_number),
    CONSTRAINT fk_posted_journal_line_header
        FOREIGN KEY (tenant_id, company_id, journal_id)
        REFERENCES accounting.posted_journal (tenant_id, company_id, journal_id),
    CONSTRAINT ck_posted_journal_line_number CHECK (line_number > 0),
    CONSTRAINT ck_posted_journal_line_amount
        CHECK (debit >= 0 AND credit >= 0 AND ((debit > 0) <> (credit > 0))),
    CONSTRAINT ck_posted_journal_line_dimensions CHECK (jsonb_typeof(dimensions) = 'array'),
    CONSTRAINT ck_posted_journal_line_currency
        CHECK (currency_snapshot IS NULL OR jsonb_typeof(currency_snapshot) = 'object')
);

CREATE INDEX ix_posted_journal_scope_effective
    ON accounting.posted_journal (tenant_id, company_id, effective_date, journal_id);
CREATE INDEX ix_posted_journal_line_scope_account
    ON accounting.posted_journal_line (tenant_id, company_id, account_id, journal_id);

ALTER TABLE accounting.posted_journal OWNER TO kagu_erp_schema_owner;
ALTER TABLE accounting.posted_journal_line OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE accounting.posted_journal, accounting.posted_journal_line FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE accounting.posted_journal, accounting.posted_journal_line TO kagu_erp_app;

ALTER TABLE accounting.posted_journal ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.posted_journal FORCE ROW LEVEL SECURITY;
ALTER TABLE accounting.posted_journal_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.posted_journal_line FORCE ROW LEVEL SECURITY;
CREATE POLICY posted_journal_scope_policy ON accounting.posted_journal
    FOR ALL TO kagu_erp_app
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY posted_journal_owner_policy ON accounting.posted_journal
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY posted_journal_line_scope_policy ON accounting.posted_journal_line
    FOR ALL TO kagu_erp_app
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY posted_journal_line_owner_policy ON accounting.posted_journal_line
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
