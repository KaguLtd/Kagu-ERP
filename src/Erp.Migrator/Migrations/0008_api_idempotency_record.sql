CREATE TABLE platform.idempotency_record
(
    record_id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    actor_id uuid NOT NULL,
    command_name varchar(160) NOT NULL,
    idempotency_key varchar(200) NOT NULL,
    request_hash char(64) NOT NULL,
    record_status smallint NOT NULL DEFAULT 1,
    response_status integer NULL,
    response_body jsonb NULL,
    aggregate_id uuid NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    completed_at timestamptz NULL,
    CONSTRAINT fk_idempotency_record_company
        FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT uq_idempotency_record_scope_key
        UNIQUE (tenant_id, company_id, actor_id, command_name, idempotency_key),
    CONSTRAINT ck_idempotency_record_command CHECK (btrim(command_name) <> ''),
    CONSTRAINT ck_idempotency_record_key CHECK (btrim(idempotency_key) <> ''),
    CONSTRAINT ck_idempotency_record_hash CHECK (request_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_idempotency_record_status CHECK (record_status IN (1, 2)),
    CONSTRAINT ck_idempotency_record_response_status
        CHECK (response_status IS NULL OR response_status BETWEEN 100 AND 599),
    CONSTRAINT ck_idempotency_record_completion
        CHECK
        (
            (record_status = 1 AND response_status IS NULL AND response_body IS NULL AND completed_at IS NULL)
            OR
            (record_status = 2 AND response_status IS NOT NULL AND response_body IS NOT NULL AND completed_at IS NOT NULL)
        )
);

CREATE INDEX ix_idempotency_record_retention
    ON platform.idempotency_record (created_at, record_id);

ALTER TABLE platform.idempotency_record OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE platform.idempotency_record FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE ON TABLE platform.idempotency_record TO kagu_erp_app;

ALTER TABLE platform.idempotency_record ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform.idempotency_record FORCE ROW LEVEL SECURITY;
CREATE POLICY idempotency_record_scope_policy ON platform.idempotency_record
    FOR ALL
    TO kagu_erp_app
    USING
    (
        tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
        AND actor_id = nullif(current_setting('app.actor_id', true), '')::uuid
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
        AND actor_id = nullif(current_setting('app.actor_id', true), '')::uuid
        AND company_id = ANY
        (
            coalesce(
                nullif(current_setting('app.company_ids', true), '')::uuid[],
                ARRAY[]::uuid[]
            )
        )
    );
CREATE POLICY idempotency_record_owner_policy ON platform.idempotency_record
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
