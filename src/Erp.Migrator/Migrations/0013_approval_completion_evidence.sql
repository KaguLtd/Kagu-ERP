CREATE SCHEMA IF NOT EXISTS workflow AUTHORIZATION kagu_erp_schema_owner;
ALTER SCHEMA workflow OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA workflow FROM PUBLIC;
GRANT USAGE ON SCHEMA workflow TO kagu_erp_app;

CREATE TABLE workflow.approval_completion_snapshot
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    approval_instance_id uuid NOT NULL,
    workflow_version_id uuid NOT NULL,
    subject_type varchar(120) NOT NULL,
    subject_id uuid NOT NULL,
    subject_version bigint NOT NULL,
    maker_id uuid NOT NULL,
    required_quorum integer NOT NULL,
    completed_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_approval_completion_snapshot
        PRIMARY KEY (tenant_id, company_id, approval_instance_id),
    CONSTRAINT fk_approval_completion_company
        FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT uq_approval_completion_subject_version
        UNIQUE (tenant_id, company_id, subject_type, subject_id, subject_version),
    CONSTRAINT ck_approval_completion_subject_type CHECK (btrim(subject_type) <> ''),
    CONSTRAINT ck_approval_completion_subject_version CHECK (subject_version > 0),
    CONSTRAINT ck_approval_completion_quorum CHECK (required_quorum > 0)
);

CREATE TABLE workflow.approval_decision_snapshot
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    approval_instance_id uuid NOT NULL,
    decision_id uuid NOT NULL,
    approver_id uuid NOT NULL,
    decided_at timestamptz NOT NULL,
    CONSTRAINT pk_approval_decision_snapshot
        PRIMARY KEY (tenant_id, company_id, approval_instance_id, decision_id),
    CONSTRAINT fk_approval_decision_completion
        FOREIGN KEY (tenant_id, company_id, approval_instance_id)
        REFERENCES workflow.approval_completion_snapshot (tenant_id, company_id, approval_instance_id),
    CONSTRAINT uq_approval_decision_distinct_approver
        UNIQUE (tenant_id, company_id, approval_instance_id, approver_id)
);

ALTER TABLE workflow.approval_completion_snapshot OWNER TO kagu_erp_schema_owner;
ALTER TABLE workflow.approval_decision_snapshot OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE workflow.approval_completion_snapshot, workflow.approval_decision_snapshot FROM PUBLIC;
GRANT SELECT ON TABLE workflow.approval_completion_snapshot, workflow.approval_decision_snapshot TO kagu_erp_app;

ALTER TABLE workflow.approval_completion_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE workflow.approval_completion_snapshot FORCE ROW LEVEL SECURITY;
ALTER TABLE workflow.approval_decision_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE workflow.approval_decision_snapshot FORCE ROW LEVEL SECURITY;

CREATE POLICY approval_completion_scope_policy ON workflow.approval_completion_snapshot
    FOR SELECT TO kagu_erp_app USING
    (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
     AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY approval_completion_owner_policy ON workflow.approval_completion_snapshot
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY approval_decision_scope_policy ON workflow.approval_decision_snapshot
    FOR SELECT TO kagu_erp_app USING
    (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
     AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY approval_decision_owner_policy ON workflow.approval_decision_snapshot
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
