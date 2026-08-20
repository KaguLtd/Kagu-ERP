CREATE SCHEMA IF NOT EXISTS iam AUTHORIZATION kagu_erp_schema_owner;
ALTER SCHEMA iam OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA iam FROM PUBLIC;
GRANT USAGE ON SCHEMA iam TO kagu_erp_app;

ALTER TABLE org.company
    ADD CONSTRAINT uq_company_tenant_id_id UNIQUE (tenant_id, id);

CREATE TABLE iam.user_profile
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    issuer varchar(320) NOT NULL,
    subject_id varchar(255) NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT fk_user_profile_tenant FOREIGN KEY (tenant_id) REFERENCES org.tenant (id),
    CONSTRAINT ck_user_profile_issuer_not_blank CHECK (btrim(issuer) <> ''),
    CONSTRAINT ck_user_profile_subject_not_blank CHECK (btrim(subject_id) <> ''),
    CONSTRAINT ck_user_profile_version_positive CHECK (version > 0),
    CONSTRAINT uq_user_profile_identity UNIQUE (issuer, subject_id),
    CONSTRAINT uq_user_profile_tenant_id_id UNIQUE (tenant_id, id)
);

CREATE TABLE iam.user_company_permission
(
    user_profile_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    permission_code varchar(120) NOT NULL,
    valid_from timestamptz NOT NULL DEFAULT '-infinity',
    valid_to timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_user_company_permission
        PRIMARY KEY (user_profile_id, company_id, permission_code),
    CONSTRAINT fk_user_company_permission_profile
        FOREIGN KEY (tenant_id, user_profile_id)
        REFERENCES iam.user_profile (tenant_id, id),
    CONSTRAINT fk_user_company_permission_company
        FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_user_company_permission_code
        CHECK (permission_code ~ '^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$'),
    CONSTRAINT ck_user_company_permission_validity
        CHECK (valid_to IS NULL OR valid_to > valid_from)
);

CREATE INDEX ix_user_company_permission_profile_validity
    ON iam.user_company_permission (user_profile_id, valid_from, valid_to, company_id);

ALTER TABLE iam.user_profile OWNER TO kagu_erp_schema_owner;
ALTER TABLE iam.user_company_permission OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE iam.user_profile, iam.user_company_permission FROM PUBLIC;
GRANT SELECT ON TABLE iam.user_profile, iam.user_company_permission TO kagu_erp_app;

ALTER TABLE iam.user_profile ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.user_profile FORCE ROW LEVEL SECURITY;
CREATE POLICY user_profile_identity_policy ON iam.user_profile
    FOR SELECT
    TO kagu_erp_app
    USING
    (
        issuer = nullif(current_setting('app.identity_issuer', true), '')
        AND subject_id = nullif(current_setting('app.identity_subject', true), '')
    );
CREATE POLICY user_profile_schema_owner_policy ON iam.user_profile
    FOR ALL
    TO kagu_erp_schema_owner
    USING (true)
    WITH CHECK (true);

ALTER TABLE iam.user_company_permission ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.user_company_permission FORCE ROW LEVEL SECURITY;
CREATE POLICY user_company_permission_identity_policy ON iam.user_company_permission
    FOR SELECT
    TO kagu_erp_app
    USING
    (
        EXISTS
        (
            SELECT 1
            FROM iam.user_profile profile
            WHERE profile.id = user_profile_id
              AND profile.tenant_id = tenant_id
              AND profile.issuer = nullif(current_setting('app.identity_issuer', true), '')
              AND profile.subject_id = nullif(current_setting('app.identity_subject', true), '')
              AND profile.is_active
        )
    );
CREATE POLICY user_company_permission_schema_owner_policy ON iam.user_company_permission
    FOR ALL
    TO kagu_erp_schema_owner
    USING (true)
    WITH CHECK (true);

ALTER DEFAULT PRIVILEGES FOR ROLE kagu_erp_schema_owner IN SCHEMA iam
    REVOKE ALL ON TABLES FROM PUBLIC;
