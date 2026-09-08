CREATE TABLE inventory.reservation_lifecycle_event
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    reservation_id uuid NOT NULL,
    event_id uuid NOT NULL,
    version bigint NOT NULL,
    transition smallint NOT NULL,
    quantity numeric(20,6) NOT NULL,
    consumed_quantity numeric(20,6) NOT NULL,
    remaining_quantity numeric(20,6) NOT NULL,
    effective_date date NOT NULL,
    occurred_at timestamptz NOT NULL,
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    actor_id uuid NOT NULL,
    correlation_id uuid NOT NULL,
    reason varchar(1000),
    CONSTRAINT pk_reservation_lifecycle_event PRIMARY KEY (tenant_id,company_id,reservation_id,version),
    CONSTRAINT uq_reservation_lifecycle_event_id UNIQUE (tenant_id,company_id,event_id),
    CONSTRAINT uq_reservation_lifecycle_retry UNIQUE (tenant_id,company_id,reservation_id,correlation_id),
    CONSTRAINT fk_reservation_lifecycle_creation FOREIGN KEY (tenant_id,company_id,reservation_id)
        REFERENCES inventory.reservation_creation (tenant_id,company_id,reservation_id),
    CONSTRAINT fk_reservation_lifecycle_actor FOREIGN KEY (tenant_id,actor_id)
        REFERENCES iam.user_profile (tenant_id,id),
    CONSTRAINT ck_reservation_lifecycle_values CHECK
        (version>=2 AND consumed_quantity>=0 AND remaining_quantity>=0
         AND ((transition=1 AND quantity>0)
              OR (transition=2 AND quantity=0 AND reason IS NOT NULL AND btrim(reason)<>''))),
    CONSTRAINT ck_reservation_lifecycle_ids CHECK
        (event_id<>'00000000-0000-0000-0000-000000000000'::uuid
         AND correlation_id<>'00000000-0000-0000-0000-000000000000'::uuid)
);

CREATE FUNCTION inventory.assert_reservation_lifecycle() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, inventory
AS $$
DECLARE
    prior_remaining numeric;
    prior_consumed numeric;
    prior_time timestamptz;
    prior_date date;
BEGIN
    IF NEW.version=2 THEN
        SELECT reserved_quantity,0,recorded_at,effective_date
          INTO prior_remaining,prior_consumed,prior_time,prior_date
        FROM inventory.reservation_creation
        WHERE tenant_id=NEW.tenant_id AND company_id=NEW.company_id AND reservation_id=NEW.reservation_id;
    ELSE
        SELECT remaining_quantity,consumed_quantity,occurred_at,effective_date
          INTO prior_remaining,prior_consumed,prior_time,prior_date
        FROM inventory.reservation_lifecycle_event
        WHERE tenant_id=NEW.tenant_id AND company_id=NEW.company_id
          AND reservation_id=NEW.reservation_id AND version=NEW.version-1;
    END IF;
    IF prior_remaining IS NULL OR prior_remaining<=0 OR NEW.occurred_at<prior_time
       OR NEW.effective_date<prior_date THEN
        RAISE EXCEPTION 'Reservation lifecycle requires an active predecessor in temporal order.' USING ERRCODE='23514';
    END IF;
    IF NEW.transition=1 THEN
        IF NEW.quantity>prior_remaining OR NEW.consumed_quantity<>prior_consumed+NEW.quantity
           OR NEW.remaining_quantity<>prior_remaining-NEW.quantity THEN
            RAISE EXCEPTION 'Reservation consumption must preserve exact quantity.' USING ERRCODE='23514';
        END IF;
    ELSIF NEW.transition=2 THEN
        IF NEW.remaining_quantity<>0 OR NEW.consumed_quantity<>prior_consumed THEN
            RAISE EXCEPTION 'Reservation release preserves consumption and clears only remaining quantity.' USING ERRCODE='23514';
        END IF;
    END IF;
    RETURN NEW;
END
$$;
CREATE TRIGGER trg_reservation_lifecycle_validate BEFORE INSERT ON inventory.reservation_lifecycle_event
FOR EACH ROW EXECUTE FUNCTION inventory.assert_reservation_lifecycle();
CREATE TRIGGER trg_reservation_lifecycle_immutable BEFORE UPDATE OR DELETE ON inventory.reservation_lifecycle_event
FOR EACH ROW EXECUTE FUNCTION inventory.guard_reservation_creation_immutable();

ALTER TABLE inventory.reservation_lifecycle_event OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.assert_reservation_lifecycle() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE inventory.reservation_lifecycle_event FROM PUBLIC,kagu_erp_app;
GRANT SELECT ON TABLE inventory.reservation_lifecycle_event TO kagu_erp_app;
ALTER TABLE inventory.reservation_lifecycle_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.reservation_lifecycle_event FORCE ROW LEVEL SECURITY;
CREATE POLICY reservation_lifecycle_scope_policy ON inventory.reservation_lifecycle_event FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY reservation_lifecycle_owner_policy ON inventory.reservation_lifecycle_event FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);
