CREATE TABLE accounting.exchange_rate_snapshot
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    rate_snapshot_id uuid NOT NULL,
    version bigint NOT NULL,
    transaction_currency char(3) NOT NULL,
    functional_currency char(3) NOT NULL,
    rate_type varchar(80) NOT NULL,
    source varchar(160) NOT NULL,
    rate_date date NOT NULL,
    functional_units_numerator numeric(28,12) NOT NULL,
    transaction_units_denominator numeric(28,12) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_exchange_rate_snapshot PRIMARY KEY (tenant_id, company_id, rate_snapshot_id),
    CONSTRAINT fk_exchange_rate_company FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_exchange_rate_version CHECK (version > 0),
    CONSTRAINT ck_exchange_rate_currency CHECK
        (transaction_currency ~ '^[A-Z]{3}$' AND functional_currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_exchange_rate_text CHECK (btrim(rate_type) <> '' AND btrim(source) <> ''),
    CONSTRAINT ck_exchange_rate_values CHECK
        (functional_units_numerator > 0 AND transaction_units_denominator > 0)
);

CREATE TABLE accounting.rounding_policy_snapshot
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    policy_id uuid NOT NULL,
    version bigint NOT NULL,
    scale smallint NOT NULL,
    rounding_mode smallint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_rounding_policy_snapshot PRIMARY KEY (tenant_id, company_id, policy_id),
    CONSTRAINT fk_rounding_policy_company FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_rounding_policy_version CHECK (version > 0),
    CONSTRAINT ck_rounding_policy_scale CHECK (scale BETWEEN 0 AND 28),
    CONSTRAINT ck_rounding_policy_mode CHECK (rounding_mode BETWEEN 1 AND 5)
);

ALTER TABLE accounting.exchange_rate_snapshot OWNER TO kagu_erp_schema_owner;
ALTER TABLE accounting.rounding_policy_snapshot OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE accounting.exchange_rate_snapshot, accounting.rounding_policy_snapshot FROM PUBLIC;
GRANT SELECT ON TABLE accounting.exchange_rate_snapshot, accounting.rounding_policy_snapshot TO kagu_erp_app;
ALTER TABLE accounting.exchange_rate_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.exchange_rate_snapshot FORCE ROW LEVEL SECURITY;
ALTER TABLE accounting.rounding_policy_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.rounding_policy_snapshot FORCE ROW LEVEL SECURITY;
CREATE POLICY exchange_rate_scope_policy ON accounting.exchange_rate_snapshot
    FOR SELECT TO kagu_erp_app USING
    (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
     AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY exchange_rate_owner_policy ON accounting.exchange_rate_snapshot
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY rounding_policy_scope_policy ON accounting.rounding_policy_snapshot
    FOR SELECT TO kagu_erp_app USING
    (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
     AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY rounding_policy_owner_policy ON accounting.rounding_policy_snapshot
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
