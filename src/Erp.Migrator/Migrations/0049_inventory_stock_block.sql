CREATE TABLE inventory.stock_block_event
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    block_id uuid NOT NULL,
    version bigint NOT NULL,
    item_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    base_uom_code varchar(16) NOT NULL,
    blocked_quantity numeric(20,6) NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    recorded_by uuid NOT NULL,
    correlation_id uuid NOT NULL,
    reason varchar(1000) NOT NULL,
    CONSTRAINT pk_stock_block_event PRIMARY KEY (tenant_id,company_id,block_id,version),
    CONSTRAINT uq_stock_block_event_retry UNIQUE (tenant_id,company_id,block_id,correlation_id),
    CONSTRAINT fk_stock_block_event_item_company FOREIGN KEY (tenant_id,company_id,item_id)
        REFERENCES inventory.item_company (tenant_id,company_id,item_id),
    CONSTRAINT fk_stock_block_event_item_uom FOREIGN KEY (tenant_id,item_id,base_uom_code)
        REFERENCES inventory.item (tenant_id,item_id,base_uom_code),
    CONSTRAINT fk_stock_block_event_warehouse FOREIGN KEY (tenant_id,company_id,warehouse_id)
        REFERENCES org.warehouse (tenant_id,company_id,warehouse_id),
    CONSTRAINT fk_stock_block_event_actor FOREIGN KEY (tenant_id,recorded_by)
        REFERENCES iam.user_profile (tenant_id,id),
    CONSTRAINT ck_stock_block_event_value CHECK
        (version>0 AND blocked_quantity>=0 AND (version<>1 OR blocked_quantity>0)
         AND btrim(reason)<>''),
    CONSTRAINT ck_stock_block_event_ids CHECK
        (block_id<>'00000000-0000-0000-0000-000000000000'::uuid
         AND correlation_id<>'00000000-0000-0000-0000-000000000000'::uuid)
);
CREATE INDEX ix_stock_block_event_position
    ON inventory.stock_block_event (tenant_id,company_id,item_id,warehouse_id,base_uom_code,block_id,version DESC);

CREATE FUNCTION inventory.assert_stock_block_event() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, inventory
AS $$
DECLARE prior inventory.stock_block_event%ROWTYPE;
BEGIN
    IF NEW.version=1 THEN RETURN NEW; END IF;
    SELECT * INTO prior FROM inventory.stock_block_event
    WHERE tenant_id=NEW.tenant_id AND company_id=NEW.company_id
      AND block_id=NEW.block_id AND version=NEW.version-1;
    IF prior.block_id IS NULL OR prior.blocked_quantity<=0
       OR NEW.blocked_quantity>=prior.blocked_quantity
       OR NEW.item_id<>prior.item_id OR NEW.warehouse_id<>prior.warehouse_id
       OR NEW.base_uom_code<>prior.base_uom_code
       OR NEW.effective_date<prior.effective_date OR NEW.recorded_at<prior.recorded_at THEN
        RAISE EXCEPTION 'Stock block release requires a matching active predecessor and lower remaining quantity.'
            USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END
$$;
CREATE TRIGGER trg_stock_block_event_validate BEFORE INSERT ON inventory.stock_block_event
FOR EACH ROW EXECUTE FUNCTION inventory.assert_stock_block_event();
CREATE TRIGGER trg_stock_block_event_immutable BEFORE UPDATE OR DELETE ON inventory.stock_block_event
FOR EACH ROW EXECUTE FUNCTION inventory.guard_reservation_creation_immutable();

ALTER TABLE inventory.stock_block_event OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.assert_stock_block_event() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE inventory.stock_block_event FROM PUBLIC,kagu_erp_app;
GRANT SELECT ON TABLE inventory.stock_block_event TO kagu_erp_app;
ALTER TABLE inventory.stock_block_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.stock_block_event FORCE ROW LEVEL SECURITY;
CREATE POLICY stock_block_event_scope_policy ON inventory.stock_block_event FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY stock_block_event_owner_policy ON inventory.stock_block_event FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);
