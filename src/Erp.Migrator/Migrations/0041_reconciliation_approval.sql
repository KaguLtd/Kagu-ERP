CREATE TABLE treasury.reconciliation_approval
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    reconciliation_id uuid NOT NULL,
    approval_instance_id uuid NOT NULL,
    workflow_version_id uuid NOT NULL,
    decision_id uuid NOT NULL,
    maker_id uuid NOT NULL,
    approver_id uuid NOT NULL,
    approved_at timestamptz NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    CONSTRAINT pk_reconciliation_approval
        PRIMARY KEY (tenant_id, company_id, reconciliation_id),
    CONSTRAINT uq_reconciliation_approval_instance
        UNIQUE (tenant_id, company_id, approval_instance_id),
    CONSTRAINT fk_reconciliation_approval_proposal
        FOREIGN KEY (tenant_id, company_id, reconciliation_id)
        REFERENCES treasury.reconciliation_proposal (tenant_id, company_id, reconciliation_id),
    CONSTRAINT fk_reconciliation_approval_completion
        FOREIGN KEY (tenant_id, company_id, approval_instance_id)
        REFERENCES workflow.approval_completion_snapshot (tenant_id, company_id, approval_instance_id),
    CONSTRAINT fk_reconciliation_approval_decision
        FOREIGN KEY (tenant_id, company_id, approval_instance_id, decision_id)
        REFERENCES workflow.approval_decision_snapshot
            (tenant_id, company_id, approval_instance_id, decision_id),
    CONSTRAINT ck_reconciliation_approval_maker_checker CHECK (maker_id <> approver_id)
);

CREATE TABLE treasury.reconciliation_approved_statement
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    statement_line_id uuid NOT NULL,
    reconciliation_id uuid NOT NULL,
    CONSTRAINT pk_reconciliation_approved_statement
        PRIMARY KEY (tenant_id, company_id, statement_line_id),
    CONSTRAINT fk_reconciliation_approved_statement_header
        FOREIGN KEY (tenant_id, company_id, reconciliation_id)
        REFERENCES treasury.reconciliation_approval (tenant_id, company_id, reconciliation_id),
    CONSTRAINT fk_reconciliation_approved_statement_line
        FOREIGN KEY (tenant_id, company_id, statement_line_id)
        REFERENCES treasury.statement_line (tenant_id, company_id, statement_line_id)
);

CREATE TABLE treasury.reconciliation_approved_movement
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    movement_id uuid NOT NULL,
    reconciliation_id uuid NOT NULL,
    CONSTRAINT pk_reconciliation_approved_movement
        PRIMARY KEY (tenant_id, company_id, movement_id),
    CONSTRAINT fk_reconciliation_approved_movement_header
        FOREIGN KEY (tenant_id, company_id, reconciliation_id)
        REFERENCES treasury.reconciliation_approval (tenant_id, company_id, reconciliation_id),
    CONSTRAINT fk_reconciliation_approved_payment
        FOREIGN KEY (tenant_id, company_id, movement_id)
        REFERENCES treasury.payment_economic_event (tenant_id, company_id, payment_id)
);

CREATE FUNCTION treasury.enforce_reconciliation_approval_snapshot()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, treasury, workflow
AS $$
DECLARE
    target_tenant uuid := coalesce(NEW.tenant_id, OLD.tenant_id);
    target_company uuid := coalesce(NEW.company_id, OLD.company_id);
    target_id uuid := coalesce(NEW.reconciliation_id, OLD.reconciliation_id);
    approval treasury.reconciliation_approval%ROWTYPE;
    invalid_approval_count bigint;
    expected_statement_count bigint;
    actual_statement_count bigint;
    expected_movement_count bigint;
    actual_movement_count bigint;
    statement_tolerance_violations bigint;
    movement_tolerance_violations bigint;
    payment_violations bigint;
BEGIN
    SELECT * INTO approval
    FROM treasury.reconciliation_approval
    WHERE tenant_id=target_tenant AND company_id=target_company AND reconciliation_id=target_id;
    IF NOT FOUND THEN RETURN NULL; END IF;

    SELECT count(*) INTO invalid_approval_count
    FROM workflow.approval_completion_snapshot completion
    JOIN workflow.approval_decision_snapshot decision
      ON decision.tenant_id=completion.tenant_id
     AND decision.company_id=completion.company_id
     AND decision.approval_instance_id=completion.approval_instance_id
    JOIN treasury.reconciliation_proposal proposal
      ON proposal.tenant_id=approval.tenant_id
     AND proposal.company_id=approval.company_id
     AND proposal.reconciliation_id=approval.reconciliation_id
    WHERE completion.tenant_id=approval.tenant_id
      AND completion.company_id=approval.company_id
      AND completion.approval_instance_id=approval.approval_instance_id
      AND completion.workflow_version_id=approval.workflow_version_id
      AND completion.subject_type='treasury.reconciliation-proposal'
      AND completion.subject_id=approval.reconciliation_id
      AND completion.subject_version=1
      AND completion.maker_id=proposal.recorded_by
      AND completion.maker_id=approval.maker_id
      AND completion.required_quorum=1
      AND decision.decision_id=approval.decision_id
      AND decision.approver_id=approval.approver_id
      AND decision.decided_at=approval.approved_at;

    SELECT count(DISTINCT statement_line_id), count(DISTINCT movement_id)
    INTO expected_statement_count, expected_movement_count
    FROM treasury.reconciliation_proposal_match
    WHERE tenant_id=target_tenant AND company_id=target_company AND reconciliation_id=target_id;

    SELECT count(*) INTO actual_statement_count
    FROM treasury.reconciliation_approved_statement
    WHERE tenant_id=target_tenant AND company_id=target_company AND reconciliation_id=target_id;

    SELECT count(*) INTO actual_movement_count
    FROM treasury.reconciliation_approved_movement
    WHERE tenant_id=target_tenant AND company_id=target_company AND reconciliation_id=target_id;

    SELECT count(*) INTO statement_tolerance_violations
    FROM (
        SELECT match.statement_line_id
        FROM treasury.reconciliation_proposal_match match
        JOIN treasury.statement_line statement
          USING (tenant_id, company_id, statement_line_id)
        WHERE match.tenant_id=target_tenant
          AND match.company_id=target_company
          AND match.reconciliation_id=target_id
        GROUP BY match.statement_line_id, statement.signed_amount
        HAVING sum(match.matched_amount) <> abs(statement.signed_amount)
    ) violations;

    SELECT count(*) INTO movement_tolerance_violations
    FROM (
        SELECT movement_id
        FROM treasury.reconciliation_proposal_match
        WHERE tenant_id=target_tenant AND company_id=target_company AND reconciliation_id=target_id
        GROUP BY movement_id
        HAVING sum(matched_amount) <> max(movement_usable_amount)
    ) violations;

    SELECT count(*) INTO payment_violations
    FROM treasury.reconciliation_proposal_match match
    LEFT JOIN treasury.payment_economic_event payment
      ON payment.tenant_id=match.tenant_id
     AND payment.company_id=match.company_id
     AND payment.payment_id=match.movement_id
    JOIN treasury.reconciliation_proposal proposal
      ON proposal.tenant_id=match.tenant_id
     AND proposal.company_id=match.company_id
     AND proposal.reconciliation_id=match.reconciliation_id
    WHERE match.tenant_id=target_tenant
      AND match.company_id=target_company
      AND match.reconciliation_id=target_id
      AND (payment.payment_id IS NULL
        OR match.movement_version <> 1
        OR payment.treasury_account_id <> proposal.treasury_account_id
        OR payment.transaction_currency <> proposal.currency
        OR payment.direction <> match.movement_direction
        OR payment.transaction_amount <> match.movement_usable_amount);

    IF invalid_approval_count <> 1
       OR actual_statement_count <> expected_statement_count
       OR actual_movement_count <> expected_movement_count
       OR statement_tolerance_violations <> 0
       OR movement_tolerance_violations <> 0
       OR payment_violations <> 0 THEN
        RAISE EXCEPTION 'Approved reconciliation does not match its proposal, payment or approval evidence.'
            USING ERRCODE='23514', CONSTRAINT='ck_reconciliation_approval_snapshot';
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER reconciliation_approval_header_guard
AFTER INSERT OR UPDATE OR DELETE ON treasury.reconciliation_approval
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION treasury.enforce_reconciliation_approval_snapshot();
CREATE CONSTRAINT TRIGGER reconciliation_approved_statement_guard
AFTER INSERT OR UPDATE OR DELETE ON treasury.reconciliation_approved_statement
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION treasury.enforce_reconciliation_approval_snapshot();
CREATE CONSTRAINT TRIGGER reconciliation_approved_movement_guard
AFTER INSERT OR UPDATE OR DELETE ON treasury.reconciliation_approved_movement
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION treasury.enforce_reconciliation_approval_snapshot();

ALTER TABLE treasury.reconciliation_approval OWNER TO kagu_erp_schema_owner;
ALTER TABLE treasury.reconciliation_approved_statement OWNER TO kagu_erp_schema_owner;
ALTER TABLE treasury.reconciliation_approved_movement OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION treasury.enforce_reconciliation_approval_snapshot() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE treasury.reconciliation_approval,
    treasury.reconciliation_approved_statement,
    treasury.reconciliation_approved_movement FROM PUBLIC;
REVOKE ALL ON FUNCTION treasury.enforce_reconciliation_approval_snapshot() FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE treasury.reconciliation_approval,
    treasury.reconciliation_approved_statement,
    treasury.reconciliation_approved_movement TO kagu_erp_app;

ALTER TABLE treasury.reconciliation_approval ENABLE ROW LEVEL SECURITY;
ALTER TABLE treasury.reconciliation_approval FORCE ROW LEVEL SECURITY;
ALTER TABLE treasury.reconciliation_approved_statement ENABLE ROW LEVEL SECURITY;
ALTER TABLE treasury.reconciliation_approved_statement FORCE ROW LEVEL SECURITY;
ALTER TABLE treasury.reconciliation_approved_movement ENABLE ROW LEVEL SECURITY;
ALTER TABLE treasury.reconciliation_approved_movement FORCE ROW LEVEL SECURITY;

CREATE POLICY reconciliation_approval_scope_policy ON treasury.reconciliation_approval
FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY reconciliation_approval_owner_policy ON treasury.reconciliation_approval
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY reconciliation_approved_statement_scope_policy ON treasury.reconciliation_approved_statement
FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY reconciliation_approved_statement_owner_policy ON treasury.reconciliation_approved_statement
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY reconciliation_approved_movement_scope_policy ON treasury.reconciliation_approved_movement
FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND
 company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY reconciliation_approved_movement_owner_policy ON treasury.reconciliation_approved_movement
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
