GRANT USAGE ON SCHEMA platform TO kagu_erp_app;

CREATE TABLE platform.audit_event
(
    id uuid PRIMARY KEY,
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    tenant_id uuid NOT NULL,
    actor_id uuid NOT NULL,
    company_ids uuid[] NOT NULL,
    correlation_id uuid NOT NULL,
    trace_id varchar(64) NOT NULL,
    session_id varchar(255) NULL,
    action varchar(160) NOT NULL,
    target_type varchar(120) NOT NULL,
    target_id varchar(160) NULL,
    outcome varchar(16) NOT NULL,
    reason_code varchar(120) NOT NULL,
    CONSTRAINT ck_audit_event_company_ids_not_empty
        CHECK (cardinality(company_ids) > 0),
    CONSTRAINT ck_audit_event_trace_not_blank
        CHECK (btrim(trace_id) <> ''),
    CONSTRAINT ck_audit_event_action_not_blank
        CHECK (btrim(action) <> ''),
    CONSTRAINT ck_audit_event_target_type_not_blank
        CHECK (btrim(target_type) <> ''),
    CONSTRAINT ck_audit_event_outcome
        CHECK (outcome IN ('allowed', 'denied')),
    CONSTRAINT ck_audit_event_reason_not_blank
        CHECK (btrim(reason_code) <> '')
);

CREATE INDEX ix_audit_event_tenant_recorded_at
    ON platform.audit_event (tenant_id, recorded_at DESC, id);
CREATE INDEX ix_audit_event_correlation_id
    ON platform.audit_event (correlation_id);

ALTER TABLE platform.audit_event OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE platform.audit_event FROM PUBLIC;
GRANT INSERT ON TABLE platform.audit_event TO kagu_erp_app;

ALTER TABLE platform.audit_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.audit_event FORCE ROW LEVEL SECURITY;
CREATE POLICY audit_event_append_policy ON platform.audit_event
    FOR INSERT
    TO kagu_erp_app
    WITH CHECK
    (
        tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND actor_id = nullif(current_setting('app.actor_id', true), '')::uuid
        AND company_ids <@ coalesce(
            nullif(current_setting('app.company_ids', true), '')::uuid[],
            ARRAY[]::uuid[])
    );
CREATE POLICY audit_event_schema_owner_policy ON platform.audit_event
    FOR ALL
    TO kagu_erp_schema_owner
    USING (true)
    WITH CHECK (true);

ALTER DEFAULT PRIVILEGES FOR ROLE kagu_erp_schema_owner IN SCHEMA platform
    REVOKE ALL ON TABLES FROM PUBLIC;
