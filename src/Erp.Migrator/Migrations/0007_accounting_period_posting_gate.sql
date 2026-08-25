CREATE TABLE accounting.accounting_period
(
    period_id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    period_code varchar(80) NOT NULL,
    starts_on date NOT NULL,
    ends_on date NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    CONSTRAINT fk_accounting_period_company
        FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT uq_accounting_period_scope_id UNIQUE (tenant_id, company_id, period_id),
    CONSTRAINT uq_accounting_period_code UNIQUE (tenant_id, company_id, period_code),
    CONSTRAINT ck_accounting_period_code CHECK (period_code = btrim(period_code) AND period_code <> ''),
    CONSTRAINT ck_accounting_period_range CHECK (starts_on <= ends_on),
    CONSTRAINT ck_accounting_period_version CHECK (version > 0)
);

CREATE TABLE accounting.period_lock_state
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    period_id uuid NOT NULL,
    lock_scope smallint NOT NULL,
    close_stage smallint NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    CONSTRAINT pk_period_lock_state PRIMARY KEY (tenant_id, company_id, period_id, lock_scope),
    CONSTRAINT fk_period_lock_state_period
        FOREIGN KEY (tenant_id, company_id, period_id)
        REFERENCES accounting.accounting_period (tenant_id, company_id, period_id),
    CONSTRAINT ck_period_lock_scope CHECK (lock_scope BETWEEN 0 AND 4),
    CONSTRAINT ck_period_close_stage CHECK (close_stage BETWEEN 0 AND 3),
    CONSTRAINT ck_period_lock_version CHECK (version > 0)
);

CREATE INDEX ix_accounting_period_effective_lookup
    ON accounting.accounting_period (tenant_id, company_id, starts_on, ends_on, period_id);

ALTER TABLE accounting.accounting_period OWNER TO kagu_erp_schema_owner;
ALTER TABLE accounting.period_lock_state OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE accounting.accounting_period, accounting.period_lock_state FROM PUBLIC;
GRANT SELECT ON TABLE accounting.accounting_period, accounting.period_lock_state TO kagu_erp_app;

ALTER TABLE accounting.accounting_period ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.accounting_period FORCE ROW LEVEL SECURITY;
ALTER TABLE accounting.period_lock_state ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.period_lock_state FORCE ROW LEVEL SECURITY;

CREATE POLICY accounting_period_scope_policy ON accounting.accounting_period
    FOR SELECT TO kagu_erp_app
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY accounting_period_owner_policy ON accounting.accounting_period
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);

CREATE POLICY period_lock_state_scope_policy ON accounting.period_lock_state
    FOR SELECT TO kagu_erp_app
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY period_lock_state_owner_policy ON accounting.period_lock_state
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
