CREATE TABLE accounting.posting_dimension_requirement_set
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    posting_rule_version_id uuid NOT NULL,
    version bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_posting_dimension_requirement_set
        PRIMARY KEY (tenant_id, company_id, posting_rule_version_id),
    CONSTRAINT fk_dimension_requirement_company
        FOREIGN KEY (tenant_id, company_id) REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_dimension_requirement_version CHECK (version > 0)
);

CREATE TABLE accounting.posting_dimension_requirement
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    posting_rule_version_id uuid NOT NULL,
    dimension_id uuid NOT NULL,
    CONSTRAINT pk_posting_dimension_requirement
        PRIMARY KEY (tenant_id, company_id, posting_rule_version_id, dimension_id),
    CONSTRAINT fk_dimension_requirement_set
        FOREIGN KEY (tenant_id, company_id, posting_rule_version_id)
        REFERENCES accounting.posting_dimension_requirement_set
            (tenant_id, company_id, posting_rule_version_id)
);

ALTER TABLE accounting.posting_dimension_requirement_set OWNER TO kagu_erp_schema_owner;
ALTER TABLE accounting.posting_dimension_requirement OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE accounting.posting_dimension_requirement_set, accounting.posting_dimension_requirement FROM PUBLIC;
GRANT SELECT ON TABLE accounting.posting_dimension_requirement_set, accounting.posting_dimension_requirement TO kagu_erp_app;

ALTER TABLE accounting.posting_dimension_requirement_set ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.posting_dimension_requirement_set FORCE ROW LEVEL SECURITY;
ALTER TABLE accounting.posting_dimension_requirement ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.posting_dimension_requirement FORCE ROW LEVEL SECURITY;
CREATE POLICY dimension_requirement_set_scope_policy ON accounting.posting_dimension_requirement_set
    FOR SELECT TO kagu_erp_app USING
    (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
     AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY dimension_requirement_set_owner_policy ON accounting.posting_dimension_requirement_set
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY dimension_requirement_scope_policy ON accounting.posting_dimension_requirement
    FOR SELECT TO kagu_erp_app USING
    (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
     AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY dimension_requirement_owner_policy ON accounting.posting_dimension_requirement
    FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
