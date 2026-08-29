CREATE TABLE reporting.aging_policy_projection_snapshot
(
 tenant_id uuid NOT NULL, company_id uuid NOT NULL, projection_generation_id uuid NOT NULL,
 policy_id uuid NOT NULL, policy_version bigint NOT NULL, bucket_count integer NOT NULL,
 CONSTRAINT pk_aging_policy_projection_snapshot PRIMARY KEY (tenant_id,company_id,projection_generation_id),
 CONSTRAINT fk_aging_policy_projection_generation FOREIGN KEY (tenant_id,company_id,projection_generation_id)
  REFERENCES reporting.projection_generation (tenant_id,company_id,projection_generation_id),
 CONSTRAINT ck_aging_policy_projection_header CHECK (policy_version>0 AND bucket_count>0)
);
CREATE TABLE reporting.aging_policy_projection_bucket
(
 tenant_id uuid NOT NULL, company_id uuid NOT NULL, projection_generation_id uuid NOT NULL,
 bucket_ordinal integer NOT NULL, bucket_code varchar(120) NOT NULL,
 minimum_days_overdue integer NOT NULL, maximum_days_overdue integer NOT NULL,
 CONSTRAINT pk_aging_policy_projection_bucket PRIMARY KEY (tenant_id,company_id,projection_generation_id,bucket_ordinal),
 CONSTRAINT uq_aging_policy_projection_bucket_code UNIQUE (tenant_id,company_id,projection_generation_id,bucket_code),
 CONSTRAINT fk_aging_policy_projection_bucket_header FOREIGN KEY (tenant_id,company_id,projection_generation_id)
  REFERENCES reporting.aging_policy_projection_snapshot (tenant_id,company_id,projection_generation_id),
 CONSTRAINT ck_aging_policy_projection_bucket CHECK
  (bucket_ordinal>0 AND bucket_code=btrim(bucket_code) AND bucket_code<>'' AND minimum_days_overdue<=maximum_days_overdue)
);
CREATE FUNCTION reporting.enforce_aging_policy_projection()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,reporting AS $$
DECLARE t uuid:=coalesce(NEW.tenant_id,OLD.tenant_id); c uuid:=coalesce(NEW.company_id,OLD.company_id);
 g uuid:=coalesce(NEW.projection_generation_id,OLD.projection_generation_id); expected integer; invalid bigint;
BEGIN
 SELECT bucket_count INTO expected FROM reporting.aging_policy_projection_snapshot
 WHERE tenant_id=t AND company_id=c AND projection_generation_id=g;
 IF NOT FOUND THEN RETURN NULL; END IF;
 IF (SELECT count(*) FROM reporting.aging_policy_projection_bucket WHERE tenant_id=t AND company_id=c AND projection_generation_id=g)<>expected THEN
  RAISE EXCEPTION 'Aging bucket count mismatch.' USING ERRCODE='23514',CONSTRAINT='ck_aging_policy_projection_bucket_count'; END IF;
 SELECT count(*) INTO invalid FROM
 (SELECT bucket_ordinal,minimum_days_overdue,maximum_days_overdue,
  lag(maximum_days_overdue) OVER (ORDER BY bucket_ordinal) previous_maximum
  FROM reporting.aging_policy_projection_bucket WHERE tenant_id=t AND company_id=c AND projection_generation_id=g) b
 WHERE (bucket_ordinal=1 AND minimum_days_overdue<>-2147483648) OR
       (bucket_ordinal>1 AND minimum_days_overdue<>previous_maximum::bigint+1) OR
       (bucket_ordinal=expected AND maximum_days_overdue<>2147483647);
 IF invalid<>0 THEN RAISE EXCEPTION 'Aging buckets are not contiguous full-range.'
  USING ERRCODE='23514',CONSTRAINT='ck_aging_policy_projection_coverage'; END IF;
 RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER aging_policy_projection_header_guard AFTER INSERT OR UPDATE ON reporting.aging_policy_projection_snapshot
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reporting.enforce_aging_policy_projection();
CREATE CONSTRAINT TRIGGER aging_policy_projection_bucket_guard AFTER INSERT OR UPDATE OR DELETE ON reporting.aging_policy_projection_bucket
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reporting.enforce_aging_policy_projection();
ALTER TABLE reporting.aging_policy_projection_snapshot OWNER TO kagu_erp_schema_owner;
ALTER TABLE reporting.aging_policy_projection_bucket OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION reporting.enforce_aging_policy_projection() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE reporting.aging_policy_projection_snapshot,reporting.aging_policy_projection_bucket FROM PUBLIC;
GRANT SELECT,INSERT ON TABLE reporting.aging_policy_projection_snapshot,reporting.aging_policy_projection_bucket TO kagu_erp_app;
ALTER TABLE reporting.aging_policy_projection_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.aging_policy_projection_snapshot FORCE ROW LEVEL SECURITY;
ALTER TABLE reporting.aging_policy_projection_bucket ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.aging_policy_projection_bucket FORCE ROW LEVEL SECURITY;
CREATE POLICY aging_policy_projection_scope ON reporting.aging_policy_projection_snapshot FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY aging_policy_projection_owner ON reporting.aging_policy_projection_snapshot FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
CREATE POLICY aging_policy_projection_bucket_scope ON reporting.aging_policy_projection_bucket FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY aging_policy_projection_bucket_owner ON reporting.aging_policy_projection_bucket FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
