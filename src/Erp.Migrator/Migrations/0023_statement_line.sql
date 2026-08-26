CREATE TABLE treasury.statement_line
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    statement_line_id uuid NOT NULL,
    statement_import_id uuid NOT NULL,
    treasury_account_id uuid NOT NULL,
    source_system varchar(120) NOT NULL,
    identity_kind varchar(80) NOT NULL,
    external_key varchar(320) NOT NULL,
    currency char(3) NOT NULL,
    signed_amount numeric(20,4) NOT NULL,
    booking_date date NOT NULL,
    value_date date NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    raw_object_sha256 char(64) NOT NULL,
    parser_version bigint NOT NULL,
    CONSTRAINT pk_statement_line PRIMARY KEY (tenant_id, company_id, statement_line_id),
    CONSTRAINT uq_statement_line_external_identity UNIQUE
        (tenant_id, company_id, treasury_account_id, source_system, identity_kind, external_key),
    CONSTRAINT fk_statement_line_company FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_statement_line_identity CHECK
        (source_system = btrim(source_system) AND source_system <> '' AND
         identity_kind = btrim(identity_kind) AND identity_kind <> '' AND
         external_key = btrim(external_key) AND external_key <> ''),
    CONSTRAINT ck_statement_line_currency CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_statement_line_amount CHECK (signed_amount <> 0),
    CONSTRAINT ck_statement_line_hash CHECK (raw_object_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_statement_line_parser CHECK (parser_version > 0)
);

ALTER TABLE treasury.statement_line OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE treasury.statement_line FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE treasury.statement_line TO kagu_erp_app;
ALTER TABLE treasury.statement_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE treasury.statement_line FORCE ROW LEVEL SECURITY;
CREATE POLICY statement_line_scope_policy ON treasury.statement_line FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY statement_line_owner_policy ON treasury.statement_line FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);
