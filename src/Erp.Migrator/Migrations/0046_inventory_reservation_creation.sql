-- Expand only. Runtime writes remain closed until the capacity/idempotency writer is ready.
CREATE TABLE inventory.reservation_creation
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    reservation_id uuid NOT NULL,
    request_id uuid NOT NULL,
    item_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    base_uom_code varchar(16) NOT NULL,
    source_type varchar(64) NOT NULL,
    source_id uuid NOT NULL,
    source_line_id uuid NOT NULL,
    source_version bigint NOT NULL,
    requested_quantity numeric(20,6) NOT NULL,
    reserved_quantity numeric(20,6) NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    recorded_by uuid NOT NULL,
    correlation_id uuid NOT NULL,
    policy_version varchar(40) NOT NULL,
    CONSTRAINT pk_reservation_creation PRIMARY KEY (tenant_id,company_id,reservation_id),
    CONSTRAINT uq_reservation_creation_request UNIQUE (tenant_id,company_id,request_id),
    CONSTRAINT fk_reservation_creation_item_company FOREIGN KEY (tenant_id,company_id,item_id)
        REFERENCES inventory.item_company (tenant_id,company_id,item_id),
    CONSTRAINT fk_reservation_creation_item_uom FOREIGN KEY (tenant_id,item_id,base_uom_code)
        REFERENCES inventory.item (tenant_id,item_id,base_uom_code),
    CONSTRAINT fk_reservation_creation_warehouse FOREIGN KEY (tenant_id,company_id,warehouse_id)
        REFERENCES org.warehouse (tenant_id,company_id,warehouse_id),
    CONSTRAINT fk_reservation_creation_actor FOREIGN KEY (tenant_id,recorded_by)
        REFERENCES iam.user_profile (tenant_id,id),
    CONSTRAINT ck_reservation_creation_quantity CHECK
        (requested_quantity > 0 AND reserved_quantity > 0 AND reserved_quantity <= requested_quantity),
    CONSTRAINT ck_reservation_creation_source CHECK
        (source_version > 0 AND source_type ~ '^[a-z0-9.-]{3,64}$'),
    CONSTRAINT ck_reservation_creation_policy CHECK
        (policy_version = btrim(policy_version) AND policy_version <> ''),
    CONSTRAINT ck_reservation_creation_ids CHECK
        (reservation_id <> '00000000-0000-0000-0000-000000000000'::uuid
         AND request_id <> '00000000-0000-0000-0000-000000000000'::uuid
         AND source_id <> '00000000-0000-0000-0000-000000000000'::uuid
         AND source_line_id <> '00000000-0000-0000-0000-000000000000'::uuid
         AND correlation_id <> '00000000-0000-0000-0000-000000000000'::uuid)
);

CREATE INDEX ix_reservation_creation_position
    ON inventory.reservation_creation (tenant_id,company_id,item_id,warehouse_id,base_uom_code);
CREATE INDEX ix_reservation_creation_demand
    ON inventory.reservation_creation (tenant_id,company_id,source_type,source_id,source_line_id);

CREATE FUNCTION inventory.guard_reservation_creation_immutable() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog
AS $$
BEGIN
    RAISE EXCEPTION 'Reservation creation is immutable; append a lifecycle event.' USING ERRCODE = '23514';
END
$$;
CREATE TRIGGER trg_reservation_creation_immutable
BEFORE UPDATE OR DELETE ON inventory.reservation_creation
FOR EACH ROW EXECUTE FUNCTION inventory.guard_reservation_creation_immutable();

ALTER TABLE inventory.reservation_creation OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.guard_reservation_creation_immutable() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE inventory.reservation_creation FROM PUBLIC, kagu_erp_app;
GRANT SELECT ON TABLE inventory.reservation_creation TO kagu_erp_app;
ALTER TABLE inventory.reservation_creation ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.reservation_creation FORCE ROW LEVEL SECURITY;
CREATE POLICY reservation_creation_scope_policy ON inventory.reservation_creation FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY reservation_creation_owner_policy ON inventory.reservation_creation FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);
