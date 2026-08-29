CREATE TABLE treasury.reconciliation_proposal
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    reconciliation_id uuid NOT NULL,
    treasury_account_id uuid NOT NULL,
    currency char(3) NOT NULL,
    match_count integer NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    CONSTRAINT pk_reconciliation_proposal PRIMARY KEY (tenant_id, company_id, reconciliation_id),
    CONSTRAINT fk_reconciliation_proposal_company FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_reconciliation_proposal_currency CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_reconciliation_proposal_count CHECK (match_count > 0)
);

CREATE TABLE treasury.reconciliation_proposal_match
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    reconciliation_id uuid NOT NULL,
    statement_line_id uuid NOT NULL,
    movement_id uuid NOT NULL,
    movement_version bigint NOT NULL,
    movement_direction smallint NOT NULL,
    movement_usable_amount numeric(20,4) NOT NULL,
    matched_amount numeric(20,4) NOT NULL,
    CONSTRAINT pk_reconciliation_proposal_match PRIMARY KEY
        (tenant_id, company_id, reconciliation_id, statement_line_id, movement_id),
    CONSTRAINT fk_reconciliation_match_header FOREIGN KEY (tenant_id, company_id, reconciliation_id)
        REFERENCES treasury.reconciliation_proposal (tenant_id, company_id, reconciliation_id),
    CONSTRAINT fk_reconciliation_match_statement FOREIGN KEY (tenant_id, company_id, statement_line_id)
        REFERENCES treasury.statement_line (tenant_id, company_id, statement_line_id),
    CONSTRAINT ck_reconciliation_match_movement CHECK
        (movement_version > 0 AND movement_direction IN (1,2) AND movement_usable_amount > 0),
    CONSTRAINT ck_reconciliation_match_amount CHECK (matched_amount > 0)
);

CREATE FUNCTION treasury.enforce_reconciliation_proposal_snapshot()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, treasury
AS $$
DECLARE
    target_tenant uuid := coalesce(NEW.tenant_id, OLD.tenant_id);
    target_company uuid := coalesce(NEW.company_id, OLD.company_id);
    target_id uuid := coalesce(NEW.reconciliation_id, OLD.reconciliation_id);
    header treasury.reconciliation_proposal%ROWTYPE;
    actual_count bigint;
    invalid_statement_count bigint;
    statement_capacity_violation_count bigint;
    movement_capacity_violation_count bigint;
BEGIN
    SELECT * INTO header FROM treasury.reconciliation_proposal
    WHERE tenant_id=target_tenant AND company_id=target_company AND reconciliation_id=target_id;
    IF NOT FOUND THEN RETURN NULL; END IF;

    SELECT count(*), count(*) FILTER (WHERE s.treasury_account_id <> header.treasury_account_id
        OR s.currency <> header.currency
        OR (s.signed_amount > 0 AND m.movement_direction <> 1)
        OR (s.signed_amount < 0 AND m.movement_direction <> 2))
    INTO actual_count, invalid_statement_count
    FROM treasury.reconciliation_proposal_match m
    JOIN treasury.statement_line s USING (tenant_id, company_id, statement_line_id)
    WHERE m.tenant_id=target_tenant AND m.company_id=target_company AND m.reconciliation_id=target_id;

    SELECT count(*) INTO statement_capacity_violation_count FROM (
        SELECT m.statement_line_id
        FROM treasury.reconciliation_proposal_match m
        JOIN treasury.statement_line s USING (tenant_id, company_id, statement_line_id)
        WHERE m.tenant_id=target_tenant AND m.company_id=target_company AND m.reconciliation_id=target_id
        GROUP BY m.statement_line_id, s.signed_amount
        HAVING sum(m.matched_amount) > abs(s.signed_amount)
    ) violations;

    SELECT count(*) INTO movement_capacity_violation_count FROM (
        SELECT movement_id
        FROM treasury.reconciliation_proposal_match
        WHERE tenant_id=target_tenant AND company_id=target_company AND reconciliation_id=target_id
        GROUP BY movement_id
        HAVING min(movement_version) <> max(movement_version)
            OR min(movement_direction) <> max(movement_direction)
            OR min(movement_usable_amount) <> max(movement_usable_amount)
            OR sum(matched_amount) > max(movement_usable_amount)
    ) violations;

    IF actual_count <> header.match_count OR invalid_statement_count <> 0
       OR statement_capacity_violation_count <> 0 OR movement_capacity_violation_count <> 0 THEN
        RAISE EXCEPTION 'Reconciliation proposal matches do not cross-foot to their immutable snapshots.'
            USING ERRCODE='23514', CONSTRAINT='ck_reconciliation_proposal_snapshot';
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER reconciliation_proposal_header_guard
AFTER INSERT OR UPDATE ON treasury.reconciliation_proposal DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION treasury.enforce_reconciliation_proposal_snapshot();
CREATE CONSTRAINT TRIGGER reconciliation_proposal_match_guard
AFTER INSERT OR UPDATE OR DELETE ON treasury.reconciliation_proposal_match DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION treasury.enforce_reconciliation_proposal_snapshot();

ALTER TABLE treasury.reconciliation_proposal OWNER TO kagu_erp_schema_owner;
ALTER TABLE treasury.reconciliation_proposal_match OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION treasury.enforce_reconciliation_proposal_snapshot() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE treasury.reconciliation_proposal, treasury.reconciliation_proposal_match FROM PUBLIC;
REVOKE ALL ON FUNCTION treasury.enforce_reconciliation_proposal_snapshot() FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE treasury.reconciliation_proposal, treasury.reconciliation_proposal_match TO kagu_erp_app;

ALTER TABLE treasury.reconciliation_proposal ENABLE ROW LEVEL SECURITY;
ALTER TABLE treasury.reconciliation_proposal FORCE ROW LEVEL SECURITY;
ALTER TABLE treasury.reconciliation_proposal_match ENABLE ROW LEVEL SECURITY;
ALTER TABLE treasury.reconciliation_proposal_match FORCE ROW LEVEL SECURITY;
CREATE POLICY reconciliation_proposal_scope_policy ON treasury.reconciliation_proposal FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY reconciliation_proposal_owner_policy ON treasury.reconciliation_proposal FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY reconciliation_match_scope_policy ON treasury.reconciliation_proposal_match FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY reconciliation_match_owner_policy ON treasury.reconciliation_proposal_match FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
