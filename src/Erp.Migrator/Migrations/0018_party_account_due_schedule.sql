CREATE SCHEMA IF NOT EXISTS party AUTHORIZATION kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA party FROM PUBLIC;
GRANT USAGE ON SCHEMA party TO kagu_erp_app;

CREATE TABLE party.party_identity
(
    tenant_id uuid NOT NULL,
    party_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    CONSTRAINT pk_party_identity PRIMARY KEY (tenant_id, party_id),
    CONSTRAINT fk_party_identity_tenant FOREIGN KEY (tenant_id) REFERENCES org.tenant (id)
);

CREATE TABLE party.party_account
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    party_account_id uuid NOT NULL,
    party_id uuid NOT NULL,
    currency char(3) NOT NULL,
    control_account_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    created_by uuid NOT NULL,
    CONSTRAINT pk_party_account PRIMARY KEY (tenant_id, company_id, party_account_id),
    CONSTRAINT uq_party_account_identity_currency UNIQUE (tenant_id, company_id, party_id, currency),
    CONSTRAINT fk_party_account_company FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT fk_party_account_party FOREIGN KEY (tenant_id, party_id)
        REFERENCES party.party_identity (tenant_id, party_id),
    CONSTRAINT ck_party_account_currency CHECK (currency ~ '^[A-Z]{3}$')
);

CREATE TABLE party.due_schedule
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    due_schedule_id uuid NOT NULL,
    party_account_id uuid NOT NULL,
    source_type varchar(120) NOT NULL,
    source_event_id uuid NOT NULL,
    source_version bigint NOT NULL,
    currency char(3) NOT NULL,
    source_original_amount numeric(20,4) NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    line_count integer NOT NULL,
    CONSTRAINT pk_due_schedule PRIMARY KEY (tenant_id, company_id, due_schedule_id),
    CONSTRAINT uq_due_schedule_source UNIQUE
        (tenant_id, company_id, source_type, source_event_id, source_version),
    CONSTRAINT fk_due_schedule_party_account FOREIGN KEY (tenant_id, company_id, party_account_id)
        REFERENCES party.party_account (tenant_id, company_id, party_account_id),
    CONSTRAINT ck_due_schedule_source CHECK
        (source_type = btrim(source_type) AND source_type <> '' AND source_version > 0),
    CONSTRAINT ck_due_schedule_currency CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_due_schedule_amount CHECK (source_original_amount > 0),
    CONSTRAINT ck_due_schedule_line_count CHECK (line_count > 0)
);

CREATE TABLE party.due_schedule_line
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    due_schedule_id uuid NOT NULL,
    due_schedule_line_id uuid NOT NULL,
    party_account_id uuid NOT NULL,
    source_event_id uuid NOT NULL,
    currency char(3) NOT NULL,
    original_amount numeric(20,4) NOT NULL,
    due_date date NOT NULL,
    payment_term_snapshot_id uuid NOT NULL,
    payment_term_version bigint NOT NULL,
    control_account_id uuid NOT NULL,
    CONSTRAINT pk_due_schedule_line PRIMARY KEY (tenant_id, company_id, due_schedule_line_id),
    CONSTRAINT uq_due_schedule_line_position UNIQUE
        (tenant_id, company_id, due_schedule_id, due_date, due_schedule_line_id),
    CONSTRAINT fk_due_schedule_line_header FOREIGN KEY (tenant_id, company_id, due_schedule_id)
        REFERENCES party.due_schedule (tenant_id, company_id, due_schedule_id),
    CONSTRAINT ck_due_schedule_line_currency CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_due_schedule_line_amount CHECK (original_amount > 0),
    CONSTRAINT ck_due_schedule_line_payment_term_version CHECK (payment_term_version > 0)
);

CREATE FUNCTION party.enforce_due_schedule_exact_total()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, party
AS $$
DECLARE
    target_tenant_id uuid := coalesce(NEW.tenant_id, OLD.tenant_id);
    target_company_id uuid := coalesce(NEW.company_id, OLD.company_id);
    target_schedule_id uuid := coalesce(NEW.due_schedule_id, OLD.due_schedule_id);
    header party.due_schedule%ROWTYPE;
    actual_count bigint;
    actual_total numeric(20,4);
    mismatched_count bigint;
BEGIN
    SELECT * INTO header FROM party.due_schedule
    WHERE tenant_id = target_tenant_id AND company_id = target_company_id
      AND due_schedule_id = target_schedule_id;
    IF NOT FOUND THEN RETURN NULL; END IF;

    SELECT count(*), coalesce(sum(original_amount), 0),
           count(*) FILTER (WHERE party_account_id <> header.party_account_id
               OR source_event_id <> header.source_event_id OR currency <> header.currency)
      INTO actual_count, actual_total, mismatched_count
    FROM party.due_schedule_line
    WHERE tenant_id = target_tenant_id AND company_id = target_company_id
      AND due_schedule_id = target_schedule_id;

    IF actual_count <> header.line_count OR actual_total <> header.source_original_amount
       OR mismatched_count <> 0 THEN
        RAISE EXCEPTION 'Due schedule lines do not cross-foot to the immutable header.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_due_schedule_exact_total';
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER due_schedule_header_total_guard
AFTER INSERT OR UPDATE ON party.due_schedule
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION party.enforce_due_schedule_exact_total();
CREATE CONSTRAINT TRIGGER due_schedule_line_total_guard
AFTER INSERT OR UPDATE OR DELETE ON party.due_schedule_line
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION party.enforce_due_schedule_exact_total();

ALTER TABLE party.party_identity OWNER TO kagu_erp_schema_owner;
ALTER TABLE party.party_account OWNER TO kagu_erp_schema_owner;
ALTER TABLE party.due_schedule OWNER TO kagu_erp_schema_owner;
ALTER TABLE party.due_schedule_line OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION party.enforce_due_schedule_exact_total() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE party.party_identity, party.party_account, party.due_schedule, party.due_schedule_line FROM PUBLIC;
REVOKE ALL ON FUNCTION party.enforce_due_schedule_exact_total() FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE party.party_identity, party.party_account, party.due_schedule, party.due_schedule_line
    TO kagu_erp_app;

ALTER TABLE party.party_identity ENABLE ROW LEVEL SECURITY;
ALTER TABLE party.party_identity FORCE ROW LEVEL SECURITY;
ALTER TABLE party.party_account ENABLE ROW LEVEL SECURITY;
ALTER TABLE party.party_account FORCE ROW LEVEL SECURITY;
ALTER TABLE party.due_schedule ENABLE ROW LEVEL SECURITY;
ALTER TABLE party.due_schedule FORCE ROW LEVEL SECURITY;
ALTER TABLE party.due_schedule_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE party.due_schedule_line FORCE ROW LEVEL SECURITY;

CREATE POLICY party_identity_scope_policy ON party.party_identity FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY party_identity_owner_policy ON party.party_identity FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);
CREATE POLICY party_account_scope_policy ON party.party_account FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY party_account_owner_policy ON party.party_account FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY due_schedule_scope_policy ON party.due_schedule FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY due_schedule_owner_policy ON party.due_schedule FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY due_schedule_line_scope_policy ON party.due_schedule_line FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY due_schedule_line_owner_policy ON party.due_schedule_line FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
