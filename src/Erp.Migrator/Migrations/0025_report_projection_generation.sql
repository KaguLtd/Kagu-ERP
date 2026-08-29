CREATE SCHEMA IF NOT EXISTS reporting AUTHORIZATION kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA reporting FROM PUBLIC;
GRANT USAGE ON SCHEMA reporting TO kagu_erp_app;

CREATE TABLE reporting.projection_generation
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    projection_generation_id uuid NOT NULL,
    report_code varchar(120) NOT NULL,
    report_definition_version bigint NOT NULL,
    effective_as_of date NOT NULL,
    data_cutoff_at timestamptz NOT NULL,
    generated_at timestamptz NOT NULL,
    currency char(3) NOT NULL,
    generation_reason varchar(160) NOT NULL,
    source_watermark_from varchar(200) NOT NULL,
    source_watermark_to varchar(200) NOT NULL,
    source_checksum_sha256 char(64) NOT NULL,
    dimension_count integer NOT NULL,
    generated_by uuid NOT NULL,
    CONSTRAINT pk_projection_generation PRIMARY KEY
        (tenant_id, company_id, projection_generation_id),
    CONSTRAINT fk_projection_generation_company FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_projection_generation_report CHECK
        (report_code=btrim(report_code) AND report_code<>'' AND report_definition_version>0),
    CONSTRAINT ck_projection_generation_time CHECK (generated_at>=data_cutoff_at),
    CONSTRAINT ck_projection_generation_currency CHECK (currency~'^[A-Z]{3}$'),
    CONSTRAINT ck_projection_generation_lineage CHECK
        (generation_reason=btrim(generation_reason) AND generation_reason<>'' AND
         source_watermark_from=btrim(source_watermark_from) AND source_watermark_from<>'' AND
         source_watermark_to=btrim(source_watermark_to) AND source_watermark_to<>'' AND
         source_checksum_sha256~'^[0-9a-f]{64}$' AND dimension_count>=0)
);

CREATE TABLE reporting.projection_generation_dimension
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    projection_generation_id uuid NOT NULL,
    dimension_code varchar(120) NOT NULL,
    value_code varchar(160) NOT NULL,
    CONSTRAINT pk_projection_generation_dimension PRIMARY KEY
        (tenant_id, company_id, projection_generation_id, dimension_code),
    CONSTRAINT fk_projection_generation_dimension FOREIGN KEY
        (tenant_id, company_id, projection_generation_id)
        REFERENCES reporting.projection_generation (tenant_id, company_id, projection_generation_id),
    CONSTRAINT ck_projection_generation_dimension CHECK
        (dimension_code=btrim(dimension_code) AND dimension_code<>'' AND
         value_code=btrim(value_code) AND value_code<>'')
);

CREATE FUNCTION reporting.enforce_projection_generation_dimensions()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,reporting AS $$
DECLARE
    target_tenant uuid:=coalesce(NEW.tenant_id,OLD.tenant_id);
    target_company uuid:=coalesce(NEW.company_id,OLD.company_id);
    target_generation uuid:=coalesce(NEW.projection_generation_id,OLD.projection_generation_id);
    expected_count integer;
    actual_count bigint;
BEGIN
    SELECT dimension_count INTO expected_count FROM reporting.projection_generation
    WHERE tenant_id=target_tenant AND company_id=target_company
      AND projection_generation_id=target_generation;
    IF NOT FOUND THEN RETURN NULL; END IF;
    SELECT count(*) INTO actual_count FROM reporting.projection_generation_dimension
    WHERE tenant_id=target_tenant AND company_id=target_company
      AND projection_generation_id=target_generation;
    IF actual_count<>expected_count THEN
        RAISE EXCEPTION 'Projection generation dimensions do not cross-foot to the manifest.'
            USING ERRCODE='23514',CONSTRAINT='ck_projection_generation_dimension_count';
    END IF;
    RETURN NULL;
END; $$;

CREATE CONSTRAINT TRIGGER projection_generation_header_guard AFTER INSERT OR UPDATE
ON reporting.projection_generation DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION reporting.enforce_projection_generation_dimensions();
CREATE CONSTRAINT TRIGGER projection_generation_dimension_guard AFTER INSERT OR UPDATE OR DELETE
ON reporting.projection_generation_dimension DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION reporting.enforce_projection_generation_dimensions();

ALTER TABLE reporting.projection_generation OWNER TO kagu_erp_schema_owner;
ALTER TABLE reporting.projection_generation_dimension OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION reporting.enforce_projection_generation_dimensions() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE reporting.projection_generation,reporting.projection_generation_dimension FROM PUBLIC;
REVOKE ALL ON FUNCTION reporting.enforce_projection_generation_dimensions() FROM PUBLIC;
GRANT SELECT,INSERT ON TABLE reporting.projection_generation,reporting.projection_generation_dimension TO kagu_erp_app;
ALTER TABLE reporting.projection_generation ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.projection_generation FORCE ROW LEVEL SECURITY;
ALTER TABLE reporting.projection_generation_dimension ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.projection_generation_dimension FORCE ROW LEVEL SECURITY;
CREATE POLICY projection_generation_scope_policy ON reporting.projection_generation FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY projection_generation_owner_policy ON reporting.projection_generation FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
CREATE POLICY projection_generation_dimension_scope_policy ON reporting.projection_generation_dimension FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY projection_generation_dimension_owner_policy ON reporting.projection_generation_dimension FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
