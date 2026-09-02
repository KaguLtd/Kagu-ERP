CREATE TABLE reporting.party_report_refresh_work_item
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    work_item_id uuid NOT NULL,
    request_key varchar(160) NOT NULL,
    request_fingerprint_sha256 varchar(64) NOT NULL,
    party_account_id uuid NOT NULL,
    report_code varchar(120) NOT NULL,
    report_definition_version bigint NOT NULL,
    effective_as_of date NOT NULL,
    recorded_cutoff timestamptz NOT NULL,
    projection_generation_id uuid NOT NULL,
    statement_id uuid NOT NULL,
    aging_report_id uuid NOT NULL,
    party_cross_foot_id uuid NOT NULL,
    control_account_reconciliation_id uuid NOT NULL,
    generated_at timestamptz NOT NULL,
    generation_reason varchar(240) NOT NULL,
    scheduled_for timestamptz NOT NULL,
    timezone_name varchar(120) NOT NULL,
    business_calendar_code varchar(120) NOT NULL,
    missed_run_policy varchar(20) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'pending',
    attempt_count integer NOT NULL DEFAULT 0,
    max_attempts integer NOT NULL DEFAULT 5,
    available_at timestamptz NOT NULL,
    lease_token uuid NULL,
    lease_expires_at timestamptz NULL,
    completed_at timestamptz NULL,
    last_error_code varchar(160) NULL,
    last_error_at timestamptz NULL,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    CONSTRAINT pk_party_report_refresh_work_item
        PRIMARY KEY (tenant_id, company_id, work_item_id),
    CONSTRAINT fk_party_report_refresh_work_item_company
        FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT uq_party_report_refresh_request
        UNIQUE (tenant_id, company_id, request_key),
    CONSTRAINT uq_party_report_refresh_generation
        UNIQUE (tenant_id, company_id, projection_generation_id),
    CONSTRAINT ck_party_report_refresh_request_key
        CHECK (btrim(request_key) <> ''),
    CONSTRAINT ck_party_report_refresh_fingerprint
        CHECK (request_fingerprint_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_party_report_refresh_report_code
        CHECK (report_code ~ '^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$'),
    CONSTRAINT ck_party_report_refresh_report_version
        CHECK (report_definition_version > 0),
    CONSTRAINT ck_party_report_refresh_generation_reason
        CHECK (btrim(generation_reason) <> ''),
    CONSTRAINT ck_party_report_refresh_timezone
        CHECK (btrim(timezone_name) <> ''),
    CONSTRAINT ck_party_report_refresh_business_calendar
        CHECK (btrim(business_calendar_code) <> ''),
    CONSTRAINT ck_party_report_refresh_missed_run_policy
        CHECK (missed_run_policy IN ('skip', 'run-once', 'catch-up')),
    CONSTRAINT ck_party_report_refresh_status
        CHECK (status IN ('pending', 'processing', 'completed', 'failed')),
    CONSTRAINT ck_party_report_refresh_attempts
        CHECK (attempt_count >= 0 AND max_attempts BETWEEN 1 AND 20 AND attempt_count <= max_attempts),
    CONSTRAINT ck_party_report_refresh_lease_state CHECK
    (
        (status = 'pending' AND lease_token IS NULL AND lease_expires_at IS NULL AND completed_at IS NULL)
        OR
        (status = 'processing' AND lease_token IS NOT NULL AND lease_expires_at IS NOT NULL
            AND completed_at IS NULL AND lease_expires_at > available_at)
        OR
        (status = 'completed' AND lease_token IS NULL AND lease_expires_at IS NULL
            AND completed_at IS NOT NULL AND last_error_code IS NULL AND last_error_at IS NULL)
        OR
        (status = 'failed' AND lease_token IS NULL AND lease_expires_at IS NULL
            AND completed_at IS NULL AND last_error_code IS NOT NULL AND last_error_at IS NOT NULL)
    ),
    CONSTRAINT ck_party_report_refresh_error_state CHECK
    (
        (last_error_code IS NULL AND last_error_at IS NULL)
        OR (last_error_code IS NOT NULL AND last_error_at IS NOT NULL)
    )
);

CREATE TABLE reporting.party_report_refresh_event
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    event_id uuid NOT NULL,
    work_item_id uuid NOT NULL,
    event_type varchar(30) NOT NULL,
    attempt_number integer NOT NULL,
    occurred_at timestamptz NOT NULL,
    actor_id uuid NOT NULL,
    lease_token uuid NULL,
    error_code varchar(160) NULL,
    CONSTRAINT pk_party_report_refresh_event
        PRIMARY KEY (tenant_id, company_id, event_id),
    CONSTRAINT fk_party_report_refresh_event_work_item
        FOREIGN KEY (tenant_id, company_id, work_item_id)
        REFERENCES reporting.party_report_refresh_work_item (tenant_id, company_id, work_item_id),
    CONSTRAINT uq_party_report_refresh_event_transition
        UNIQUE (tenant_id, company_id, work_item_id, attempt_number, event_type),
    CONSTRAINT ck_party_report_refresh_event_type
        CHECK (event_type IN ('enqueued', 'claimed', 'completed', 'retry-scheduled', 'failed')),
    CONSTRAINT ck_party_report_refresh_event_attempt
        CHECK (attempt_number >= 0),
    CONSTRAINT ck_party_report_refresh_event_payload CHECK
    (
        (event_type = 'enqueued' AND attempt_number = 0 AND lease_token IS NULL AND error_code IS NULL)
        OR
        (event_type = 'claimed' AND attempt_number > 0 AND lease_token IS NOT NULL AND error_code IS NULL)
        OR
        (event_type = 'completed' AND attempt_number > 0 AND lease_token IS NOT NULL AND error_code IS NULL)
        OR
        (event_type IN ('retry-scheduled', 'failed') AND attempt_number > 0
            AND lease_token IS NOT NULL AND error_code IS NOT NULL)
    )
);

CREATE INDEX ix_party_report_refresh_claim
    ON reporting.party_report_refresh_work_item
       (tenant_id, status, available_at, lease_expires_at, scheduled_for, work_item_id);

CREATE INDEX ix_party_report_refresh_event_work_item
    ON reporting.party_report_refresh_event
       (tenant_id, company_id, work_item_id, occurred_at, event_id);

CREATE FUNCTION reporting.enforce_party_report_refresh_transition()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF ROW(
        NEW.tenant_id, NEW.company_id, NEW.work_item_id, NEW.request_key,
        NEW.request_fingerprint_sha256, NEW.party_account_id, NEW.report_code,
        NEW.report_definition_version, NEW.effective_as_of, NEW.recorded_cutoff,
        NEW.projection_generation_id, NEW.statement_id, NEW.aging_report_id,
        NEW.party_cross_foot_id, NEW.control_account_reconciliation_id,
        NEW.generated_at, NEW.generation_reason, NEW.scheduled_for,
        NEW.timezone_name, NEW.business_calendar_code, NEW.missed_run_policy,
        NEW.max_attempts, NEW.created_at, NEW.created_by)
       IS DISTINCT FROM ROW(
        OLD.tenant_id, OLD.company_id, OLD.work_item_id, OLD.request_key,
        OLD.request_fingerprint_sha256, OLD.party_account_id, OLD.report_code,
        OLD.report_definition_version, OLD.effective_as_of, OLD.recorded_cutoff,
        OLD.projection_generation_id, OLD.statement_id, OLD.aging_report_id,
        OLD.party_cross_foot_id, OLD.control_account_reconciliation_id,
        OLD.generated_at, OLD.generation_reason, OLD.scheduled_for,
        OLD.timezone_name, OLD.business_calendar_code, OLD.missed_run_policy,
        OLD.max_attempts, OLD.created_at, OLD.created_by) THEN
        RAISE EXCEPTION 'Party report refresh request fields are immutable.'
            USING ERRCODE = '55000';
    END IF;

    IF OLD.status IN ('completed', 'failed') THEN
        RAISE EXCEPTION 'Terminal Party report refresh work cannot be changed.'
            USING ERRCODE = '55000';
    END IF;

    IF OLD.status = 'pending' THEN
        IF NEW.status <> 'processing'
           OR NEW.attempt_count <> OLD.attempt_count + 1
           OR NEW.lease_token IS NULL
           OR NEW.lease_expires_at IS NULL THEN
            RAISE EXCEPTION 'Pending Party report refresh work can only be claimed.'
                USING ERRCODE = '55000';
        END IF;
    ELSIF NEW.status = 'processing' THEN
        IF NEW.attempt_count <> OLD.attempt_count + 1
           OR OLD.lease_expires_at IS NULL
           OR NEW.lease_token IS NULL
           OR NEW.lease_token = OLD.lease_token THEN
            RAISE EXCEPTION 'Processing work can only be reclaimed with a new lease and attempt.'
                USING ERRCODE = '55000';
        END IF;
    ELSE
        IF NEW.status NOT IN ('pending', 'completed', 'failed')
           OR NEW.attempt_count <> OLD.attempt_count
           OR NEW.lease_token IS NOT NULL
           OR NEW.lease_expires_at IS NOT NULL THEN
            RAISE EXCEPTION 'Processing work has an invalid terminal or retry transition.'
                USING ERRCODE = '55000';
        END IF;
    END IF;

    RETURN NEW;
END;
$$;

CREATE TRIGGER party_report_refresh_transition_guard
BEFORE UPDATE ON reporting.party_report_refresh_work_item
FOR EACH ROW EXECUTE FUNCTION reporting.enforce_party_report_refresh_transition();

ALTER TABLE reporting.party_report_refresh_work_item OWNER TO kagu_erp_schema_owner;
ALTER TABLE reporting.party_report_refresh_event OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION reporting.enforce_party_report_refresh_transition() OWNER TO kagu_erp_schema_owner;

REVOKE ALL ON TABLE reporting.party_report_refresh_work_item,
    reporting.party_report_refresh_event FROM PUBLIC;
REVOKE ALL ON FUNCTION reporting.enforce_party_report_refresh_transition() FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE reporting.party_report_refresh_work_item TO kagu_erp_app;
GRANT UPDATE
    (status, attempt_count, available_at, lease_token, lease_expires_at,
     completed_at, last_error_code, last_error_at)
    ON reporting.party_report_refresh_work_item TO kagu_erp_app;
GRANT SELECT, INSERT ON TABLE reporting.party_report_refresh_event TO kagu_erp_app;

ALTER TABLE reporting.party_report_refresh_work_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_report_refresh_work_item FORCE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_report_refresh_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_report_refresh_event FORCE ROW LEVEL SECURITY;

CREATE POLICY party_report_refresh_work_item_scope_policy
ON reporting.party_report_refresh_work_item
FOR ALL TO kagu_erp_app
USING
(
    tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[]))
)
WITH CHECK
(
    tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[]))
);

CREATE POLICY party_report_refresh_work_item_owner_policy
ON reporting.party_report_refresh_work_item
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);

CREATE POLICY party_report_refresh_event_scope_policy
ON reporting.party_report_refresh_event
FOR ALL TO kagu_erp_app
USING
(
    tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[]))
)
WITH CHECK
(
    tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(
        nullif(current_setting('app.company_ids', true), '')::uuid[],
        ARRAY[]::uuid[]))
);

CREATE POLICY party_report_refresh_event_owner_policy
ON reporting.party_report_refresh_event
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
