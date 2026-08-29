CREATE TABLE reporting.party_aging_projection
(
 tenant_id uuid NOT NULL, company_id uuid NOT NULL, projection_generation_id uuid NOT NULL,
 aging_report_id uuid NOT NULL, party_account_id uuid NOT NULL, control_account_id uuid NOT NULL,
 balance_side smallint NOT NULL, total_remaining numeric(20,4) NOT NULL, item_count integer NOT NULL,
 CONSTRAINT pk_party_aging_projection PRIMARY KEY (tenant_id,company_id,aging_report_id),
 CONSTRAINT uq_party_aging_projection_generation_account UNIQUE (tenant_id,company_id,projection_generation_id,party_account_id),
 CONSTRAINT fk_party_aging_projection_generation FOREIGN KEY (tenant_id,company_id,projection_generation_id)
  REFERENCES reporting.projection_generation (tenant_id,company_id,projection_generation_id),
 CONSTRAINT fk_party_aging_projection_policy FOREIGN KEY (tenant_id,company_id,projection_generation_id)
  REFERENCES reporting.aging_policy_projection_snapshot (tenant_id,company_id,projection_generation_id),
 CONSTRAINT ck_party_aging_projection_header CHECK (balance_side IN (1,2) AND total_remaining>=0 AND item_count>=0)
);
CREATE TABLE reporting.party_aging_projection_item
(
 tenant_id uuid NOT NULL, company_id uuid NOT NULL, aging_report_id uuid NOT NULL,
 item_ordinal integer NOT NULL, open_item_id uuid NOT NULL, source_event_id uuid NOT NULL,
 due_schedule_line_id uuid NOT NULL, original_amount numeric(20,4) NOT NULL,
 remaining_amount numeric(20,4) NOT NULL, due_date date NOT NULL,
 is_disputed boolean NOT NULL, is_blocked boolean NOT NULL,
 CONSTRAINT pk_party_aging_projection_item PRIMARY KEY (tenant_id,company_id,aging_report_id,item_ordinal),
 CONSTRAINT uq_party_aging_projection_open_item UNIQUE (tenant_id,company_id,aging_report_id,open_item_id),
 CONSTRAINT fk_party_aging_projection_item_header FOREIGN KEY (tenant_id,company_id,aging_report_id)
  REFERENCES reporting.party_aging_projection (tenant_id,company_id,aging_report_id),
 CONSTRAINT ck_party_aging_projection_item CHECK
  (item_ordinal>0 AND original_amount>0 AND remaining_amount>0 AND remaining_amount<=original_amount)
);
CREATE FUNCTION reporting.enforce_party_aging_projection()
RETURNS trigger LANGUAGE plpgsql SET search_path=pg_catalog,reporting AS $$
DECLARE t uuid:=coalesce(NEW.tenant_id,OLD.tenant_id); c uuid:=coalesce(NEW.company_id,OLD.company_id);
 r uuid:=coalesce(NEW.aging_report_id,OLD.aging_report_id); expected_count integer; expected_total numeric(20,4);
BEGIN
 SELECT item_count,total_remaining INTO expected_count,expected_total FROM reporting.party_aging_projection
 WHERE tenant_id=t AND company_id=c AND aging_report_id=r;
 IF NOT FOUND THEN RETURN NULL; END IF;
 IF (SELECT count(*) FROM reporting.party_aging_projection_item WHERE tenant_id=t AND company_id=c AND aging_report_id=r)<>expected_count THEN
  RAISE EXCEPTION 'Aging item count mismatch.' USING ERRCODE='23514',CONSTRAINT='ck_party_aging_projection_item_count'; END IF;
 IF (SELECT coalesce(sum(remaining_amount),0) FROM reporting.party_aging_projection_item WHERE tenant_id=t AND company_id=c AND aging_report_id=r)<>expected_total THEN
  RAISE EXCEPTION 'Aging remaining total mismatch.' USING ERRCODE='23514',CONSTRAINT='ck_party_aging_projection_total'; END IF;
 RETURN NULL;
END; $$;
CREATE CONSTRAINT TRIGGER party_aging_projection_header_guard AFTER INSERT OR UPDATE ON reporting.party_aging_projection
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reporting.enforce_party_aging_projection();
CREATE CONSTRAINT TRIGGER party_aging_projection_item_guard AFTER INSERT OR UPDATE OR DELETE ON reporting.party_aging_projection_item
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reporting.enforce_party_aging_projection();
ALTER TABLE reporting.party_aging_projection OWNER TO kagu_erp_schema_owner;
ALTER TABLE reporting.party_aging_projection_item OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION reporting.enforce_party_aging_projection() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE reporting.party_aging_projection,reporting.party_aging_projection_item FROM PUBLIC;
GRANT SELECT,INSERT ON TABLE reporting.party_aging_projection,reporting.party_aging_projection_item TO kagu_erp_app;
ALTER TABLE reporting.party_aging_projection ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_aging_projection FORCE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_aging_projection_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.party_aging_projection_item FORCE ROW LEVEL SECURITY;
CREATE POLICY party_aging_projection_scope ON reporting.party_aging_projection FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY party_aging_projection_owner ON reporting.party_aging_projection FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
CREATE POLICY party_aging_projection_item_scope ON reporting.party_aging_projection_item FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY party_aging_projection_item_owner ON reporting.party_aging_projection_item FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
