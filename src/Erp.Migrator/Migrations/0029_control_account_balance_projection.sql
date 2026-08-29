CREATE TABLE reporting.control_account_balance_projection
(
 tenant_id uuid NOT NULL,
 company_id uuid NOT NULL,
 projection_generation_id uuid NOT NULL,
 snapshot_id uuid NOT NULL,
 control_account_id uuid NOT NULL,
 ledger_side smallint NOT NULL,
 opening_balance numeric(20,4) NOT NULL,
 debits numeric(20,4) NOT NULL,
 credits numeric(20,4) NOT NULL,
 closing_balance numeric(20,4) NOT NULL,
 row_count bigint NOT NULL,
 source_checksum_sha256 char(64) NOT NULL,
 CONSTRAINT pk_control_account_balance_projection PRIMARY KEY (tenant_id,company_id,snapshot_id),
 CONSTRAINT uq_control_account_balance_projection_side UNIQUE
  (tenant_id,company_id,projection_generation_id,control_account_id,ledger_side),
 CONSTRAINT fk_control_account_balance_projection_generation FOREIGN KEY
  (tenant_id,company_id,projection_generation_id)
  REFERENCES reporting.projection_generation (tenant_id,company_id,projection_generation_id),
 CONSTRAINT ck_control_account_balance_projection CHECK
  (ledger_side IN (1,2) AND debits>=0 AND credits>=0 AND row_count>=0 AND
   source_checksum_sha256~'^[0-9a-f]{64}$' AND opening_balance+debits-credits=closing_balance)
);
ALTER TABLE reporting.control_account_balance_projection OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE reporting.control_account_balance_projection FROM PUBLIC;
GRANT SELECT,INSERT ON TABLE reporting.control_account_balance_projection TO kagu_erp_app;
ALTER TABLE reporting.control_account_balance_projection ENABLE ROW LEVEL SECURITY;
ALTER TABLE reporting.control_account_balance_projection FORCE ROW LEVEL SECURITY;
CREATE POLICY control_account_balance_projection_scope ON reporting.control_account_balance_projection FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY control_account_balance_projection_owner ON reporting.control_account_balance_projection FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
