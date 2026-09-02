CREATE TABLE iam.service_identity
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    identity_code varchar(120) NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    valid_from timestamptz NOT NULL DEFAULT '-infinity',
    valid_to timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT fk_service_identity_tenant
        FOREIGN KEY (tenant_id) REFERENCES org.tenant (id),
    CONSTRAINT uq_service_identity_tenant_id
        UNIQUE (tenant_id, id),
    CONSTRAINT uq_service_identity_tenant_code
        UNIQUE (tenant_id, identity_code),
    CONSTRAINT ck_service_identity_code_not_blank
        CHECK (btrim(identity_code) <> ''),
    CONSTRAINT ck_service_identity_validity
        CHECK (valid_to IS NULL OR valid_to > valid_from)
);

CREATE TABLE iam.service_identity_company_permission
(
    service_identity_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    permission_code varchar(120) NOT NULL,
    valid_from timestamptz NOT NULL DEFAULT '-infinity',
    valid_to timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_service_identity_company_permission
        PRIMARY KEY (service_identity_id, company_id, permission_code),
    CONSTRAINT fk_service_identity_company_permission_identity
        FOREIGN KEY (tenant_id, service_identity_id)
        REFERENCES iam.service_identity (tenant_id, id),
    CONSTRAINT fk_service_identity_company_permission_company
        FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_service_identity_company_permission_code
        CHECK (permission_code ~ '^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$'),
    CONSTRAINT ck_service_identity_company_permission_validity
        CHECK (valid_to IS NULL OR valid_to > valid_from)
);

CREATE INDEX ix_service_identity_company_permission_active
    ON iam.service_identity_company_permission
       (tenant_id, service_identity_id, permission_code, company_id, valid_from, valid_to);

CREATE FUNCTION iam.reject_reused_principal_id()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, iam
AS $$
BEGIN
    IF TG_TABLE_NAME = 'service_identity' THEN
        IF EXISTS (SELECT 1 FROM iam.user_profile WHERE id = NEW.id) THEN
            RAISE EXCEPTION 'A service identity cannot reuse a human user-profile ID.'
                USING ERRCODE = '23505', CONSTRAINT = 'uq_iam_principal_id';
        END IF;
    ELSIF EXISTS (SELECT 1 FROM iam.service_identity WHERE id = NEW.id) THEN
        RAISE EXCEPTION 'A human user profile cannot reuse a service-identity ID.'
            USING ERRCODE = '23505', CONSTRAINT = 'uq_iam_principal_id';
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER service_identity_principal_id_guard
BEFORE INSERT OR UPDATE OF id ON iam.service_identity
FOR EACH ROW EXECUTE FUNCTION iam.reject_reused_principal_id();

CREATE TRIGGER user_profile_principal_id_guard
BEFORE INSERT OR UPDATE OF id ON iam.user_profile
FOR EACH ROW EXECUTE FUNCTION iam.reject_reused_principal_id();

ALTER TABLE iam.service_identity OWNER TO kagu_erp_schema_owner;
ALTER TABLE iam.service_identity_company_permission OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION iam.reject_reused_principal_id() OWNER TO kagu_erp_schema_owner;

REVOKE ALL ON TABLE iam.service_identity,
    iam.service_identity_company_permission FROM PUBLIC;
REVOKE ALL ON FUNCTION iam.reject_reused_principal_id() FROM PUBLIC;
GRANT SELECT ON TABLE iam.service_identity,
    iam.service_identity_company_permission TO kagu_erp_app;

ALTER TABLE iam.service_identity ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.service_identity FORCE ROW LEVEL SECURITY;
ALTER TABLE iam.service_identity_company_permission ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.service_identity_company_permission FORCE ROW LEVEL SECURITY;

CREATE POLICY service_identity_self_policy
ON iam.service_identity
FOR SELECT TO kagu_erp_app
USING
(
    tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND id = nullif(current_setting('app.actor_id', true), '')::uuid
);

CREATE POLICY service_identity_owner_policy
ON iam.service_identity
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);

CREATE POLICY service_identity_company_permission_self_policy
ON iam.service_identity_company_permission
FOR SELECT TO kagu_erp_app
USING
(
    tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND service_identity_id = nullif(current_setting('app.actor_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[]))
);

CREATE POLICY service_identity_company_permission_owner_policy
ON iam.service_identity_company_permission
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
