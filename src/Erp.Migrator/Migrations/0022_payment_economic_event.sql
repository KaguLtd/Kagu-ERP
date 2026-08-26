CREATE SCHEMA IF NOT EXISTS treasury AUTHORIZATION kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA treasury FROM PUBLIC;
GRANT USAGE ON SCHEMA treasury TO kagu_erp_app;

CREATE TABLE treasury.payment_economic_event
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    payment_id uuid NOT NULL,
    party_account_id uuid NOT NULL,
    treasury_account_id uuid NOT NULL,
    direction smallint NOT NULL,
    transaction_amount numeric(20,4) NOT NULL,
    functional_amount numeric(20,4) NOT NULL,
    transaction_currency char(3) NOT NULL,
    functional_currency char(3) NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    source_type varchar(120) NOT NULL,
    source_event_id uuid NOT NULL,
    posting_purpose varchar(120) NOT NULL,
    rate_snapshot_id uuid NOT NULL,
    rate_version bigint NOT NULL,
    rate_type varchar(80) NOT NULL,
    rate_source varchar(160) NOT NULL,
    rate_date date NOT NULL,
    functional_units_numerator numeric(20,10) NOT NULL,
    transaction_units_denominator numeric(20,10) NOT NULL,
    CONSTRAINT pk_payment_economic_event PRIMARY KEY (tenant_id, company_id, payment_id),
    CONSTRAINT uq_payment_economic_event_source UNIQUE
        (tenant_id, company_id, source_type, source_event_id, posting_purpose),
    CONSTRAINT fk_payment_economic_event_company FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_payment_economic_event_direction CHECK (direction IN (1,2)),
    CONSTRAINT ck_payment_economic_event_amount CHECK
        (transaction_amount > 0 AND transaction_amount = functional_amount),
    CONSTRAINT ck_payment_economic_event_currency CHECK
        (transaction_currency ~ '^[A-Z]{3}$' AND transaction_currency = functional_currency),
    CONSTRAINT ck_payment_economic_event_source CHECK
        (source_type = btrim(source_type) AND source_type <> '' AND
         posting_purpose = btrim(posting_purpose) AND posting_purpose <> ''),
    CONSTRAINT ck_payment_economic_event_rate CHECK
        (rate_version > 0 AND rate_type = btrim(rate_type) AND rate_type <> '' AND
         rate_source = btrim(rate_source) AND rate_source <> '' AND
         functional_units_numerator > 0 AND
         functional_units_numerator = transaction_units_denominator)
);

ALTER TABLE treasury.payment_economic_event OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE treasury.payment_economic_event FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE treasury.payment_economic_event TO kagu_erp_app;
ALTER TABLE treasury.payment_economic_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE treasury.payment_economic_event FORCE ROW LEVEL SECURITY;
CREATE POLICY payment_economic_event_scope_policy ON treasury.payment_economic_event FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY payment_economic_event_owner_policy ON treasury.payment_economic_event
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
