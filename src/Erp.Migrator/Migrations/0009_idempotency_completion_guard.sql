REVOKE UPDATE ON TABLE platform.idempotency_record FROM kagu_erp_app;
GRANT UPDATE (record_status, response_status, response_body, aggregate_id, completed_at)
    ON TABLE platform.idempotency_record TO kagu_erp_app;

CREATE FUNCTION platform.guard_idempotency_completion()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.record_id <> NEW.record_id
       OR OLD.tenant_id <> NEW.tenant_id
       OR OLD.company_id <> NEW.company_id
       OR OLD.actor_id <> NEW.actor_id
       OR OLD.command_name <> NEW.command_name
       OR OLD.idempotency_key <> NEW.idempotency_key
       OR OLD.request_hash <> NEW.request_hash
       OR OLD.created_at <> NEW.created_at THEN
        RAISE EXCEPTION 'Idempotency identity and request fields are immutable.'
            USING ERRCODE = '23514';
    END IF;

    IF OLD.record_status <> 1 OR NEW.record_status <> 2 THEN
        RAISE EXCEPTION 'Idempotency records only allow in-progress to completed transition.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$$;

ALTER FUNCTION platform.guard_idempotency_completion() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON FUNCTION platform.guard_idempotency_completion() FROM PUBLIC;

CREATE TRIGGER trg_idempotency_completion_guard
BEFORE UPDATE ON platform.idempotency_record
FOR EACH ROW EXECUTE FUNCTION platform.guard_idempotency_completion();
