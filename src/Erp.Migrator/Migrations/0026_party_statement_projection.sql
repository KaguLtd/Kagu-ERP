CREATE TABLE reporting.party_statement_projection
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    projection_generation_id uuid NOT NULL,
    statement_id uuid NOT NULL,
    party_account_id uuid NOT NULL,
    control_account_id uuid NOT NULL,
    balance_side smallint NOT NULL,
    opening_exposure numeric(20,4) NOT NULL,
    closing_exposure numeric(20,4) NOT NULL,
    line_count integer NOT NULL,
    CONSTRAINT pk_party_statement_projection PRIMARY KEY (tenant_id,company_id,statement_id),
    CONSTRAINT uq_party_statement_projection_generation_account UNIQUE
        (tenant_id,company_id,projection_generation_id,party_account_id),
    CONSTRAINT fk_party_statement_projection_generation FOREIGN KEY
        (tenant_id,company_id,projection_generation_id)
        REFERENCES reporting.projection_generation (tenant_id,company_id,projection_generation_id),
    CONSTRAINT ck_party_statement_projection_values CHECK
        (balance_side IN (1,2) AND opening_exposure>=0 AND closing_exposure>=0 AND line_count>=0)
);

CREATE TABLE reporting.party_statement_projection_line
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    statement_id uuid NOT NULL,
    line_number integer NOT NULL,
    event_id uuid NOT NULL,
    event_kind smallint NOT NULL,
    source_type varchar(120) NOT NULL,
    source_event_id uuid NOT NULL,
    due_schedule_line_id uuid NOT NULL,
    payment_id uuid NULL,
    exposure_effect numeric(20,4) NOT NULL,
    running_exposure numeric(20,4) NOT NULL,
    effective_date date NOT NULL,
    sequence_key bigint NOT NULL,
    recorded_at timestamptz NOT NULL,
    CONSTRAINT pk_party_statement_projection_line PRIMARY KEY (tenant_id,company_id,statement_id,line_number),
    CONSTRAINT uq_party_statement_projection_event UNIQUE (tenant_id,event_id),
    CONSTRAINT fk_party_statement_projection_line_header FOREIGN KEY (tenant_id,company_id,statement_id)
        REFERENCES reporting.party_statement_projection (tenant_id,company_id,statement_id),
    CONSTRAINT ck_party_statement_projection_line_values CHECK
        (line_number>0 AND event_kind BETWEEN 1 AND 5 AND source_type=btrim(source_type) AND source_type<>'' AND
         exposure_effect<>0 AND running_exposure>=0 AND sequence_key>0 AND
         ((event_kind IN (2,3) AND payment_id IS NOT NULL) OR (event_kind NOT IN (2,3) AND payment_id IS NULL)))
);

CREATE FUNCTION reporting.enforce_party_statement_projection()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,reporting AS $$
DECLARE
    target_tenant uuid:=coalesce(NEW.tenant_id,OLD.tenant_id);
    target_company uuid:=coalesce(NEW.company_id,OLD.company_id);
    target_statement uuid:=coalesce(NEW.statement_id,OLD.statement_id);
    expected_count integer;
    expected_closing numeric(20,4);
    invalid_running bigint;
BEGIN
    SELECT line_count,closing_exposure INTO expected_count,expected_closing
    FROM reporting.party_statement_projection
    WHERE tenant_id=target_tenant AND company_id=target_company AND statement_id=target_statement;
    IF NOT FOUND THEN RETURN NULL; END IF;
    IF (SELECT count(*) FROM reporting.party_statement_projection_line
        WHERE tenant_id=target_tenant AND company_id=target_company AND statement_id=target_statement)<>expected_count THEN
        RAISE EXCEPTION 'Party statement line count does not cross-foot.'
            USING ERRCODE='23514',CONSTRAINT='ck_party_statement_projection_line_count';
    END IF;
    IF (SELECT opening_exposure+coalesce(sum(exposure_effect),0)
        FROM reporting.party_statement_projection p LEFT JOIN reporting.party_statement_projection_line l
          USING (tenant_id,company_id,statement_id)
        WHERE p.tenant_id=target_tenant AND p.company_id=target_company AND p.statement_id=target_statement
        GROUP BY opening_exposure)<>expected_closing THEN
        RAISE EXCEPTION 'Party statement closing exposure does not cross-foot.'
            USING ERRCODE='23514',CONSTRAINT='ck_party_statement_projection_closing';
    END IF;
    SELECT count(*) INTO invalid_running FROM
    (
        SELECT l.running_exposure,
               p.opening_exposure+sum(l.exposure_effect) OVER (ORDER BY l.line_number) AS expected_running
        FROM reporting.party_statement_projection p JOIN reporting.party_statement_projection_line l
          USING (tenant_id,company_id,statement_id)
        WHERE p.tenant_id=target_tenant AND p.company_id=target_company AND p.statement_id=target_statement
    ) checked WHERE running_exposure<>expected_running;
    IF invalid_running<>0 THEN
        RAISE EXCEPTION 'Party statement running exposure does not cross-foot.'
            USING ERRCODE='23514',CONSTRAINT='ck_party_statement_projection_running';
    END IF;
    RETURN NULL;
END; $$;

CREATE CONSTRAINT TRIGGER party_statement_projection_header_guard AFTER INSERT OR UPDATE
ON reporting.party_statement_projection DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION reporting.enforce_party_statement_projection();
CREATE CONSTRAINT TRIGGER party_statement_projection_line_guard AFTER INSERT OR UPDATE OR DELETE
ON reporting.party_statement_projection_line DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION reporting.enforce_party_statement_projection();

ALTER TABLE reporting.party_statement_projection OWNER TO kagu_erp_schema_owner;
ALTER TABLE reporting.party_statement_projection_line OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION reporting.enforce_party_statement_projection() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE reporting.party_statement_projection,reporting.party_statement_projection_line FROM PUBLIC;
REVOKE ALL ON FUNCTION reporting.enforce_party_statement_projection() FROM PUBLIC;
GRANT SELECT,INSERT ON TABLE reporting.party_statement_projection,reporting.party_statement_projection_line TO kagu_erp_app;
ALTER TABLE reporting.party_statement_projection ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_statement_projection FORCE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_statement_projection_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_statement_projection_line FORCE ROW LEVEL SECURITY;
CREATE POLICY party_statement_projection_scope_policy ON reporting.party_statement_projection FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY party_statement_projection_owner_policy ON reporting.party_statement_projection FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
CREATE POLICY party_statement_projection_line_scope_policy ON reporting.party_statement_projection_line FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY party_statement_projection_line_owner_policy ON reporting.party_statement_projection_line FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
