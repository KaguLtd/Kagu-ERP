CREATE SCHEMA IF NOT EXISTS purchasing AUTHORIZATION kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA purchasing FROM PUBLIC;
GRANT USAGE ON SCHEMA purchasing TO kagu_erp_app;

CREATE TABLE purchasing.invoice_capture (
    tenant_id uuid NOT NULL, company_id uuid NOT NULL, invoice_id uuid NOT NULL,
    party_account_id uuid NOT NULL, document_number varchar(120) NOT NULL, document_date date NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version=1),
    currency char(3) NOT NULL, line_count integer NOT NULL, total_net_amount numeric(20,4) NOT NULL,
    fingerprint char(64) NOT NULL, recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(), recorded_by uuid NOT NULL,
    PRIMARY KEY (tenant_id,company_id,invoice_id),
    FOREIGN KEY (tenant_id,company_id) REFERENCES org.company(tenant_id,id),
    FOREIGN KEY (tenant_id,company_id,party_account_id) REFERENCES party.party_account(tenant_id,company_id,party_account_id),
    FOREIGN KEY (tenant_id,recorded_by) REFERENCES iam.user_profile(tenant_id,user_profile_id),
    CHECK (document_number=btrim(document_number) AND length(document_number)>0 AND document_number !~ '[[:cntrl:]]'),
    CHECK (currency ~ '^[A-Z]{3}$' AND line_count BETWEEN 1 AND 500 AND total_net_amount>=0),
    CHECK (isfinite(document_date) AND isfinite(recorded_at) AND fingerprint ~ '^[0-9a-f]{64}$'),
    CHECK (invoice_id<>'00000000-0000-0000-0000-000000000000'::uuid)
);
CREATE TABLE purchasing.invoice_capture_line (
    tenant_id uuid NOT NULL, company_id uuid NOT NULL, invoice_id uuid NOT NULL, line_id uuid NOT NULL,
    item_id uuid NOT NULL, base_uom_code varchar(16) NOT NULL, base_quantity numeric(20,6) NOT NULL, net_amount numeric(20,4) NOT NULL,
    PRIMARY KEY (tenant_id,company_id,invoice_id,line_id),
    FOREIGN KEY (tenant_id,company_id,invoice_id) REFERENCES purchasing.invoice_capture(tenant_id,company_id,invoice_id),
    FOREIGN KEY (tenant_id,company_id,item_id) REFERENCES inventory.item_company(tenant_id,company_id,item_id),
    FOREIGN KEY (tenant_id,item_id,base_uom_code) REFERENCES inventory.item(tenant_id,item_id,base_uom_code),
    CHECK (base_quantity>0 AND net_amount>=0 AND base_uom_code ~ '^[A-Z0-9][A-Z0-9-]{0,15}$'),
    CHECK (line_id<>'00000000-0000-0000-0000-000000000000'::uuid)
);
CREATE FUNCTION purchasing.guard_invoice_capture_immutable() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog,purchasing AS $$
BEGIN
    RAISE EXCEPTION 'Invoice capture snapshots are immutable.' USING ERRCODE='55000';
END;
$$;
CREATE TRIGGER trg_invoice_capture_immutable BEFORE UPDATE OR DELETE ON purchasing.invoice_capture
FOR EACH ROW EXECUTE FUNCTION purchasing.guard_invoice_capture_immutable();
CREATE TRIGGER trg_invoice_capture_line_immutable BEFORE UPDATE OR DELETE ON purchasing.invoice_capture_line
FOR EACH ROW EXECUTE FUNCTION purchasing.guard_invoice_capture_immutable();
CREATE FUNCTION purchasing.assert_invoice_capture_complete() RETURNS trigger
LANGUAGE plpgsql SET search_path=pg_catalog,purchasing AS $$
DECLARE expected integer; expected_amount numeric; actual bigint; actual_amount numeric;
BEGIN
    SELECT line_count,total_net_amount INTO expected,expected_amount FROM purchasing.invoice_capture
    WHERE tenant_id=NEW.tenant_id AND company_id=NEW.company_id AND invoice_id=NEW.invoice_id;
    SELECT count(*),coalesce(sum(net_amount),0) INTO actual,actual_amount FROM purchasing.invoice_capture_line
    WHERE tenant_id=NEW.tenant_id AND company_id=NEW.company_id AND invoice_id=NEW.invoice_id;
    IF expected IS NULL OR expected<>actual OR expected_amount<>actual_amount THEN
        RAISE EXCEPTION 'Invoice capture requires its complete exact line set and net total.' USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER trg_invoice_capture_complete AFTER INSERT ON purchasing.invoice_capture
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION purchasing.assert_invoice_capture_complete();
CREATE CONSTRAINT TRIGGER trg_invoice_capture_line_complete AFTER INSERT ON purchasing.invoice_capture_line
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION purchasing.assert_invoice_capture_complete();
ALTER TABLE purchasing.invoice_capture OWNER TO kagu_erp_schema_owner;
ALTER TABLE purchasing.invoice_capture_line OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION purchasing.guard_invoice_capture_immutable() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION purchasing.assert_invoice_capture_complete() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE purchasing.invoice_capture,purchasing.invoice_capture_line FROM PUBLIC,kagu_erp_app;
GRANT SELECT ON TABLE purchasing.invoice_capture,purchasing.invoice_capture_line TO kagu_erp_app;
ALTER TABLE purchasing.invoice_capture ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.invoice_capture FORCE ROW LEVEL SECURITY;
ALTER TABLE purchasing.invoice_capture_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE purchasing.invoice_capture_line FORCE ROW LEVEL SECURITY;
CREATE POLICY invoice_capture_scope ON purchasing.invoice_capture FOR SELECT TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY invoice_capture_line_scope ON purchasing.invoice_capture_line FOR SELECT TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
 AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY invoice_capture_owner ON purchasing.invoice_capture FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
CREATE POLICY invoice_capture_line_owner ON purchasing.invoice_capture_line FOR ALL TO kagu_erp_schema_owner USING(true) WITH CHECK(true);
