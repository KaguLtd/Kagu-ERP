CREATE TABLE platform.outbox_message
(
    event_id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    aggregate_type varchar(120) NOT NULL,
    aggregate_id uuid NOT NULL,
    aggregate_sequence bigint NOT NULL,
    event_type varchar(160) NOT NULL,
    schema_version integer NOT NULL,
    occurred_at timestamptz NOT NULL,
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    payload jsonb NOT NULL,
    message_hash char(64) NOT NULL,
    status varchar(24) NOT NULL DEFAULT 'pending',
    attempt_count integer NOT NULL DEFAULT 0,
    available_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    lease_id uuid NULL,
    lease_until timestamptz NULL,
    processed_at timestamptz NULL,
    last_error_code varchar(120) NULL,
    CONSTRAINT uq_outbox_aggregate_sequence
        UNIQUE (tenant_id, company_id, aggregate_type, aggregate_id, aggregate_sequence),
    CONSTRAINT ck_outbox_aggregate_type_not_blank CHECK (btrim(aggregate_type) <> ''),
    CONSTRAINT ck_outbox_event_type_not_blank CHECK (btrim(event_type) <> ''),
    CONSTRAINT ck_outbox_aggregate_sequence_positive CHECK (aggregate_sequence > 0),
    CONSTRAINT ck_outbox_schema_version_positive CHECK (schema_version > 0),
    CONSTRAINT ck_outbox_message_hash CHECK (message_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_outbox_status CHECK
        (status IN ('pending', 'processing', 'processed', 'requires_action', 'dead_letter')),
    CONSTRAINT ck_outbox_attempt_count_nonnegative CHECK (attempt_count >= 0),
    CONSTRAINT ck_outbox_lease_pair CHECK
        ((lease_id IS NULL) = (lease_until IS NULL)),
    CONSTRAINT ck_outbox_processing_lease CHECK
        (status <> 'processing' OR (lease_id IS NOT NULL AND lease_until IS NOT NULL)),
    CONSTRAINT ck_outbox_processed_at CHECK
        ((status = 'processed' AND processed_at IS NOT NULL) OR
         (status <> 'processed' AND processed_at IS NULL))
);

CREATE INDEX ix_outbox_dispatch
    ON platform.outbox_message (available_at, occurred_at, event_id)
    WHERE status IN ('pending', 'processing');
CREATE INDEX ix_outbox_tenant_company_status
    ON platform.outbox_message (tenant_id, company_id, status, available_at);

ALTER TABLE platform.outbox_message OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE platform.outbox_message FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE platform.outbox_message TO kagu_erp_app;

ALTER TABLE platform.outbox_message ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.outbox_message FORCE ROW LEVEL SECURITY;
CREATE POLICY outbox_message_scope_policy ON platform.outbox_message
    FOR ALL
    TO kagu_erp_app
    USING
    (
        tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND company_id = ANY
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
        AND company_id = ANY
        (
            coalesce(
                nullif(current_setting('app.company_ids', true), '')::uuid[],
                ARRAY[]::uuid[]
            )
        )
    );
CREATE POLICY outbox_message_schema_owner_policy ON platform.outbox_message
    FOR ALL
    TO kagu_erp_schema_owner
    USING (true)
    WITH CHECK (true);
