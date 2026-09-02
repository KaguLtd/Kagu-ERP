-- The runtime role is intentionally append-only and therefore has no UPDATE
-- privilege. Replace the row-locking stream guard with a transaction advisory
-- lock so it stays SECURITY INVOKER and cannot observe rows outside caller RLS.
CREATE OR REPLACE FUNCTION reporting.enforce_aging_policy_definition_stream()
RETURNS trigger
LANGUAGE plpgsql
SECURITY INVOKER
SET search_path = pg_catalog, reporting
AS $$
DECLARE
    latest reporting.aging_policy_definition%ROWTYPE;
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(
        'aging-policy:' || NEW.tenant_id::text || ':' || NEW.company_id::text,
        527614));

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
    LIMIT 1;

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

ALTER FUNCTION reporting.enforce_aging_policy_definition_stream() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON FUNCTION reporting.enforce_aging_policy_definition_stream() FROM PUBLIC;
