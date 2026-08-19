CREATE SCHEMA IF NOT EXISTS org AUTHORIZATION kagu_erp_schema_owner;
ALTER SCHEMA org OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA org FROM PUBLIC;
GRANT USAGE ON SCHEMA org TO kagu_erp_app;

CREATE TABLE org.tenant
(
    id uuid PRIMARY KEY,
    code varchar(40) NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_tenant_code_not_blank CHECK (btrim(code) <> ''),
    CONSTRAINT ck_tenant_version_positive CHECK (version > 0),
    CONSTRAINT uq_tenant_code UNIQUE (code)
);

CREATE TABLE org.company
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    code varchar(40) NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT fk_company_tenant FOREIGN KEY (tenant_id) REFERENCES org.tenant (id),
    CONSTRAINT ck_company_code_not_blank CHECK (btrim(code) <> ''),
    CONSTRAINT ck_company_version_positive CHECK (version > 0),
    CONSTRAINT uq_company_tenant_code UNIQUE (tenant_id, code)
);

CREATE INDEX ix_company_tenant_id_id ON org.company (tenant_id, id);

ALTER TABLE org.tenant OWNER TO kagu_erp_schema_owner;
ALTER TABLE org.company OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE org.tenant, org.company FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE org.tenant, org.company TO kagu_erp_app;

ALTER TABLE org.tenant ENABLE ROW LEVEL SECURITY;
ALTER TABLE org.tenant FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_scope_policy ON org.tenant
    FOR ALL
    TO kagu_erp_app
    USING (id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_schema_owner_policy ON org.tenant
    FOR ALL
    TO kagu_erp_schema_owner
    USING (true)
    WITH CHECK (true);

ALTER TABLE org.company ENABLE ROW LEVEL SECURITY;
ALTER TABLE org.company FORCE ROW LEVEL SECURITY;
CREATE POLICY company_scope_policy ON org.company
    FOR ALL
    TO kagu_erp_app
    USING
    (
        tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND id = ANY
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
        AND id = ANY
        (
            coalesce(
                nullif(current_setting('app.company_ids', true), '')::uuid[],
                ARRAY[]::uuid[]
            )
        )
    );
CREATE POLICY company_schema_owner_policy ON org.company
    FOR ALL
    TO kagu_erp_schema_owner
    USING (true)
    WITH CHECK (true);

ALTER DEFAULT PRIVILEGES FOR ROLE kagu_erp_schema_owner IN SCHEMA org
    REVOKE ALL ON TABLES FROM PUBLIC;
