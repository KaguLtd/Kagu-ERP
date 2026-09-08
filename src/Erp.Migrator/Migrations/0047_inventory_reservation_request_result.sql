CREATE TABLE inventory.reservation_request_result
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    request_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    request_fingerprint varchar(64) NOT NULL,
    requested_quantity numeric(20,6) NOT NULL,
    reserved_quantity numeric(20,6) NOT NULL,
    reservation_id uuid,
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    recorded_by uuid NOT NULL,
    CONSTRAINT pk_reservation_request_result PRIMARY KEY (tenant_id,company_id,request_id),
    CONSTRAINT fk_reservation_request_result_warehouse FOREIGN KEY (tenant_id,company_id,warehouse_id)
        REFERENCES org.warehouse (tenant_id,company_id,warehouse_id),
    CONSTRAINT fk_reservation_request_result_actor FOREIGN KEY (tenant_id,recorded_by)
        REFERENCES iam.user_profile (tenant_id,id),
    CONSTRAINT ck_reservation_request_result_fingerprint CHECK (request_fingerprint ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_reservation_request_result_quantity CHECK
        (requested_quantity > 0 AND reserved_quantity >= 0 AND reserved_quantity <= requested_quantity
         AND ((reserved_quantity=0 AND reservation_id IS NULL)
              OR (reserved_quantity>0 AND reservation_id IS NOT NULL))),
    CONSTRAINT ck_reservation_request_result_id CHECK
        (request_id <> '00000000-0000-0000-0000-000000000000'::uuid)
);

ALTER TABLE inventory.reservation_creation ADD CONSTRAINT uq_reservation_creation_result_link
    UNIQUE (tenant_id,company_id,request_id,reservation_id,warehouse_id,requested_quantity,reserved_quantity);
ALTER TABLE inventory.reservation_request_result ADD CONSTRAINT fk_reservation_request_result_creation
    FOREIGN KEY (tenant_id,company_id,request_id,reservation_id,warehouse_id,requested_quantity,reserved_quantity)
    REFERENCES inventory.reservation_creation
        (tenant_id,company_id,request_id,reservation_id,warehouse_id,requested_quantity,reserved_quantity)
    DEFERRABLE INITIALLY DEFERRED;
ALTER TABLE inventory.reservation_creation ADD CONSTRAINT fk_reservation_creation_result
    FOREIGN KEY (tenant_id,company_id,request_id)
    REFERENCES inventory.reservation_request_result (tenant_id,company_id,request_id)
    DEFERRABLE INITIALLY DEFERRED;

-- A zero outcome cannot coexist with a positive creation for the same request.
CREATE FUNCTION inventory.assert_reservation_request_pair() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, inventory
AS $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM inventory.reservation_creation c
        JOIN inventory.reservation_request_result r
          USING (tenant_id,company_id,request_id)
        WHERE c.tenant_id=NEW.tenant_id AND c.company_id=NEW.company_id AND c.request_id=NEW.request_id
          AND r.reservation_id IS DISTINCT FROM c.reservation_id
    ) THEN
        RAISE EXCEPTION 'Reservation request result must match its creation.' USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END
$$;
CREATE CONSTRAINT TRIGGER trg_reservation_creation_result_pair
AFTER INSERT ON inventory.reservation_creation DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION inventory.assert_reservation_request_pair();
CREATE CONSTRAINT TRIGGER trg_reservation_request_result_pair
AFTER INSERT ON inventory.reservation_request_result DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION inventory.assert_reservation_request_pair();
CREATE TRIGGER trg_reservation_request_result_immutable
BEFORE UPDATE OR DELETE ON inventory.reservation_request_result
FOR EACH ROW EXECUTE FUNCTION inventory.guard_reservation_creation_immutable();

ALTER TABLE inventory.reservation_request_result OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.assert_reservation_request_pair() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE inventory.reservation_request_result FROM PUBLIC,kagu_erp_app;
GRANT SELECT ON TABLE inventory.reservation_request_result TO kagu_erp_app;
ALTER TABLE inventory.reservation_request_result ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.reservation_request_result FORCE ROW LEVEL SECURITY;
CREATE POLICY reservation_request_result_scope_policy ON inventory.reservation_request_result FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY reservation_request_result_owner_policy ON inventory.reservation_request_result FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);
