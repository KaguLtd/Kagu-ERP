CREATE TABLE reporting.aging_policy_definition
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    policy_id uuid NOT NULL,
    policy_version bigint NOT NULL,
    effective_from date NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    bucket_count integer NOT NULL,
    CONSTRAINT pk_aging_policy_definition
        PRIMARY KEY (tenant_id, company_id, policy_version),
    CONSTRAINT uq_aging_policy_definition_identity
        UNIQUE (tenant_id, company_id, policy_id, policy_version),
    CONSTRAINT fk_aging_policy_definition_company
        FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_aging_policy_definition_header
        CHECK (policy_version > 0 AND bucket_count > 0)
);

CREATE TABLE reporting.aging_policy_definition_bucket
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    policy_version bigint NOT NULL,
    bucket_ordinal integer NOT NULL,
    bucket_code varchar(120) NOT NULL,
    minimum_days_overdue integer NOT NULL,
    maximum_days_overdue integer NOT NULL,
    CONSTRAINT pk_aging_policy_definition_bucket
        PRIMARY KEY (tenant_id, company_id, policy_version, bucket_ordinal),
    CONSTRAINT uq_aging_policy_definition_bucket_code
        UNIQUE (tenant_id, company_id, policy_version, bucket_code),
    CONSTRAINT fk_aging_policy_definition_bucket_header
        FOREIGN KEY (tenant_id, company_id, policy_version)
        REFERENCES reporting.aging_policy_definition (tenant_id, company_id, policy_version),
    CONSTRAINT ck_aging_policy_definition_bucket
        CHECK (bucket_ordinal > 0
            AND bucket_code = btrim(bucket_code)
            AND bucket_code <> ''
            AND minimum_days_overdue <= maximum_days_overdue)
);

CREATE FUNCTION reporting.enforce_aging_policy_definition_stream()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, reporting
AS $$
DECLARE
    latest reporting.aging_policy_definition%ROWTYPE;
BEGIN
    IF EXISTS (
        SELECT 1
        FROM reporting.aging_policy_definition existing
        WHERE existing.tenant_id = NEW.tenant_id
          AND existing.company_id = NEW.company_id
          AND existing.policy_version = NEW.policy_version
    ) THEN
        RETURN NEW;
    END IF;

    SELECT *
      INTO latest
    FROM reporting.aging_policy_definition
    WHERE tenant_id = NEW.tenant_id
      AND company_id = NEW.company_id
    ORDER BY policy_version DESC
    LIMIT 1
    FOR UPDATE;

    IF NOT FOUND THEN
        IF NEW.policy_version <> 1 THEN
            RAISE EXCEPTION 'The first aging policy version must be one.'
                USING ERRCODE = '23514',
                      CONSTRAINT = 'ck_aging_policy_definition_version_sequence';
        END IF;
        RETURN NEW;
    END IF;

    IF NEW.policy_id <> latest.policy_id THEN
        RAISE EXCEPTION 'A company aging policy stream cannot change policy identity.'
            USING ERRCODE = '23514',
                  CONSTRAINT = 'ck_aging_policy_definition_policy_id_stable';
    END IF;
    IF NEW.policy_version <> latest.policy_version + 1 THEN
        RAISE EXCEPTION 'Aging policy versions must be contiguous.'
            USING ERRCODE = '23514',
                  CONSTRAINT = 'ck_aging_policy_definition_version_sequence';
    END IF;
    IF NEW.recorded_at < latest.recorded_at THEN
        RAISE EXCEPTION 'Aging policy recorded time cannot move backwards.'
            USING ERRCODE = '23514',
                  CONSTRAINT = 'ck_aging_policy_definition_recorded_chronology';
    END IF;
    RETURN NEW;
END;
$$;

CREATE FUNCTION reporting.enforce_aging_policy_definition_buckets()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, reporting
AS $$
DECLARE
    target_tenant uuid := coalesce(NEW.tenant_id, OLD.tenant_id);
    target_company uuid := coalesce(NEW.company_id, OLD.company_id);
    target_version bigint := coalesce(NEW.policy_version, OLD.policy_version);
    expected_count integer;
    invalid_count bigint;
BEGIN
    SELECT bucket_count
      INTO expected_count
    FROM reporting.aging_policy_definition
    WHERE tenant_id = target_tenant
      AND company_id = target_company
      AND policy_version = target_version;
    IF NOT FOUND THEN
        RETURN NULL;
    END IF;

    IF (
        SELECT count(*)
        FROM reporting.aging_policy_definition_bucket
        WHERE tenant_id = target_tenant
          AND company_id = target_company
          AND policy_version = target_version
    ) <> expected_count THEN
        RAISE EXCEPTION 'Aging policy bucket count mismatch.'
            USING ERRCODE = '23514',
                  CONSTRAINT = 'ck_aging_policy_definition_bucket_count';
    END IF;

    SELECT count(*)
      INTO invalid_count
    FROM (
        SELECT
            bucket_ordinal,
            minimum_days_overdue,
            maximum_days_overdue,
            row_number() OVER (ORDER BY bucket_ordinal) AS expected_ordinal,
            lag(maximum_days_overdue) OVER (ORDER BY bucket_ordinal) AS previous_maximum
        FROM reporting.aging_policy_definition_bucket
        WHERE tenant_id = target_tenant
          AND company_id = target_company
          AND policy_version = target_version
    ) bucket
    WHERE bucket_ordinal <> expected_ordinal
       OR (expected_ordinal = 1 AND minimum_days_overdue <> -2147483648)
       OR (expected_ordinal > 1 AND minimum_days_overdue::bigint <> previous_maximum::bigint + 1)
       OR (expected_ordinal = expected_count AND maximum_days_overdue <> 2147483647);

    IF invalid_count <> 0 THEN
        RAISE EXCEPTION 'Aging policy buckets must be ordinal, contiguous and full-range.'
            USING ERRCODE = '23514',
                  CONSTRAINT = 'ck_aging_policy_definition_bucket_coverage';
    END IF;
    RETURN NULL;
END;
$$;

CREATE TRIGGER aging_policy_definition_stream_guard
BEFORE INSERT ON reporting.aging_policy_definition
FOR EACH ROW EXECUTE FUNCTION reporting.enforce_aging_policy_definition_stream();

CREATE CONSTRAINT TRIGGER aging_policy_definition_header_guard
AFTER INSERT OR UPDATE ON reporting.aging_policy_definition
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION reporting.enforce_aging_policy_definition_buckets();

CREATE CONSTRAINT TRIGGER aging_policy_definition_bucket_guard
AFTER INSERT OR UPDATE OR DELETE ON reporting.aging_policy_definition_bucket
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION reporting.enforce_aging_policy_definition_buckets();

CREATE INDEX ix_aging_policy_definition_cut
    ON reporting.aging_policy_definition
       (tenant_id, company_id, policy_version DESC, effective_from, recorded_at);

ALTER TABLE reporting.aging_policy_definition OWNER TO kagu_erp_schema_owner;
ALTER TABLE reporting.aging_policy_definition_bucket OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION reporting.enforce_aging_policy_definition_stream() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION reporting.enforce_aging_policy_definition_buckets() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE reporting.aging_policy_definition,
    reporting.aging_policy_definition_bucket FROM PUBLIC;
REVOKE ALL ON FUNCTION reporting.enforce_aging_policy_definition_stream(),
    reporting.enforce_aging_policy_definition_buckets() FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE reporting.aging_policy_definition,
    reporting.aging_policy_definition_bucket TO kagu_erp_app;

ALTER TABLE reporting.aging_policy_definition ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.aging_policy_definition FORCE ROW LEVEL SECURITY;
ALTER TABLE reporting.aging_policy_definition_bucket ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.aging_policy_definition_bucket FORCE ROW LEVEL SECURITY;

CREATE POLICY aging_policy_definition_scope_policy
ON reporting.aging_policy_definition
FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[])));
CREATE POLICY aging_policy_definition_owner_policy
ON reporting.aging_policy_definition
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);

CREATE POLICY aging_policy_definition_bucket_scope_policy
ON reporting.aging_policy_definition_bucket
FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[])));
CREATE POLICY aging_policy_definition_bucket_owner_policy
ON reporting.aging_policy_definition_bucket
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
