CREATE TABLE accounting.chart_of_accounts_version
(
    chart_version_id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    version bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT fk_chart_version_company
        FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT uq_chart_version_scope_id UNIQUE (tenant_id, company_id, chart_version_id),
    CONSTRAINT uq_chart_version_scope_version UNIQUE (tenant_id, company_id, version),
    CONSTRAINT ck_chart_version_positive CHECK (version > 0)
);

CREATE TABLE accounting.account_posting_snapshot
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    chart_version_id uuid NOT NULL,
    account_id uuid NOT NULL,
    account_kind smallint NOT NULL,
    is_active boolean NOT NULL,
    version bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_account_posting_snapshot
        PRIMARY KEY (tenant_id, company_id, chart_version_id, account_id),
    CONSTRAINT fk_account_snapshot_chart_version
        FOREIGN KEY (tenant_id, company_id, chart_version_id)
        REFERENCES accounting.chart_of_accounts_version (tenant_id, company_id, chart_version_id),
    CONSTRAINT ck_account_snapshot_kind CHECK (account_kind IN (0, 1)),
    CONSTRAINT ck_account_snapshot_version CHECK (version > 0)
);

CREATE INDEX ix_account_snapshot_scope_account
    ON accounting.account_posting_snapshot (tenant_id, company_id, account_id, chart_version_id);

ALTER TABLE accounting.chart_of_accounts_version OWNER TO kagu_erp_schema_owner;
ALTER TABLE accounting.account_posting_snapshot OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE accounting.chart_of_accounts_version, accounting.account_posting_snapshot FROM PUBLIC;
GRANT SELECT ON TABLE accounting.chart_of_accounts_version, accounting.account_posting_snapshot TO kagu_erp_app;

ALTER TABLE accounting.chart_of_accounts_version ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.chart_of_accounts_version FORCE ROW LEVEL SECURITY;
ALTER TABLE accounting.account_posting_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.account_posting_snapshot FORCE ROW LEVEL SECURITY;

CREATE POLICY chart_version_scope_policy ON accounting.chart_of_accounts_version
    FOR SELECT TO kagu_erp_app
    USING
    (
        tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[]))
    );
CREATE POLICY chart_version_owner_policy ON accounting.chart_of_accounts_version
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY account_snapshot_scope_policy ON accounting.account_posting_snapshot
    FOR SELECT TO kagu_erp_app
    USING
    (
        tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[]))
    );
CREATE POLICY account_snapshot_owner_policy ON accounting.account_posting_snapshot
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
