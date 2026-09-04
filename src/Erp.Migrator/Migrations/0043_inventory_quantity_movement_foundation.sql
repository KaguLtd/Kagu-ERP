CREATE SCHEMA IF NOT EXISTS inventory AUTHORIZATION kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA inventory FROM PUBLIC;
GRANT USAGE ON SCHEMA inventory TO kagu_erp_app;

CREATE TABLE org.warehouse
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    code varchar(40) NOT NULL,
    name varchar(160) NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_warehouse PRIMARY KEY (tenant_id, company_id, warehouse_id),
    CONSTRAINT fk_warehouse_company FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT uq_warehouse_code UNIQUE (tenant_id, company_id, code),
    CONSTRAINT ck_warehouse_code CHECK
        (code = upper(btrim(code)) AND code ~ '^[A-Z0-9][A-Z0-9._-]{0,39}$'),
    CONSTRAINT ck_warehouse_name CHECK (name = btrim(name) AND name <> ''),
    CONSTRAINT ck_warehouse_version CHECK (version > 0)
);

CREATE TABLE inventory.item
(
    tenant_id uuid NOT NULL,
    item_id uuid NOT NULL,
    code varchar(64) NOT NULL,
    name varchar(200) NOT NULL,
    kind smallint NOT NULL,
    base_uom_code varchar(16) NOT NULL,
    tracking_policy smallint NOT NULL,
    allows_fractional_quantity boolean NOT NULL,
    quantity_scale smallint NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_inventory_item PRIMARY KEY (tenant_id, item_id),
    CONSTRAINT fk_inventory_item_tenant FOREIGN KEY (tenant_id) REFERENCES org.tenant (id),
    CONSTRAINT uq_inventory_item_code UNIQUE (tenant_id, code),
    CONSTRAINT uq_inventory_item_base_uom UNIQUE (tenant_id, item_id, base_uom_code),
    CONSTRAINT ck_inventory_item_code CHECK
        (code = upper(btrim(code)) AND code ~ '^[A-Z0-9][A-Z0-9._-]{0,63}$'),
    CONSTRAINT ck_inventory_item_name CHECK (name = btrim(name) AND name <> ''),
    CONSTRAINT ck_inventory_item_kind CHECK (kind IN (1,2,3,4)),
    CONSTRAINT ck_inventory_item_uom CHECK
        (base_uom_code = upper(btrim(base_uom_code)) AND base_uom_code ~ '^[A-Z0-9][A-Z0-9-]{0,15}$'),
    CONSTRAINT ck_inventory_item_tracking CHECK
        (tracking_policy IN (1,2,3) AND (kind = 1 OR tracking_policy = 1)),
    CONSTRAINT ck_inventory_item_quantity_scale CHECK
        (quantity_scale BETWEEN 0 AND 6
         AND ((allows_fractional_quantity AND quantity_scale BETWEEN 1 AND 6)
              OR (NOT allows_fractional_quantity AND quantity_scale = 0))
         AND NOT (tracking_policy = 3 AND allows_fractional_quantity)),
    CONSTRAINT ck_inventory_item_version CHECK (version > 0)
);

CREATE TABLE iam.user_warehouse_scope
(
    user_profile_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    valid_from timestamptz NOT NULL DEFAULT '-infinity',
    valid_to timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_user_warehouse_scope
        PRIMARY KEY (user_profile_id, company_id, warehouse_id),
    CONSTRAINT fk_user_warehouse_scope_profile
        FOREIGN KEY (tenant_id, user_profile_id)
        REFERENCES iam.user_profile (tenant_id, id),
    CONSTRAINT fk_user_warehouse_scope_warehouse
        FOREIGN KEY (tenant_id, company_id, warehouse_id)
        REFERENCES org.warehouse (tenant_id, company_id, warehouse_id),
    CONSTRAINT ck_user_warehouse_scope_validity
        CHECK (valid_to IS NULL OR valid_to > valid_from)
);

CREATE TABLE inventory.item_company
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    item_id uuid NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    CONSTRAINT pk_inventory_item_company PRIMARY KEY (tenant_id, company_id, item_id),
    CONSTRAINT fk_inventory_item_company_company FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT fk_inventory_item_company_item FOREIGN KEY (tenant_id, item_id)
        REFERENCES inventory.item (tenant_id, item_id),
    CONSTRAINT ck_inventory_item_company_version CHECK (version > 0)
);

CREATE TABLE inventory.stock_movement
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    movement_id uuid NOT NULL,
    item_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    base_uom_code varchar(16) NOT NULL,
    movement_kind smallint NOT NULL,
    base_quantity numeric(20,6) NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    sequence_key bigint NOT NULL,
    source_type varchar(120) NOT NULL,
    source_event_id uuid NOT NULL,
    source_line_id uuid NOT NULL,
    source_version bigint NOT NULL,
    posting_purpose varchar(120) NOT NULL,
    transfer_id uuid,
    counterpart_warehouse_id uuid,
    reversal_of_movement_id uuid,
    CONSTRAINT pk_stock_movement PRIMARY KEY (tenant_id, company_id, movement_id),
    CONSTRAINT fk_stock_movement_item_company FOREIGN KEY (tenant_id, company_id, item_id)
        REFERENCES inventory.item_company (tenant_id, company_id, item_id),
    CONSTRAINT fk_stock_movement_item_base_uom FOREIGN KEY (tenant_id, item_id, base_uom_code)
        REFERENCES inventory.item (tenant_id, item_id, base_uom_code),
    CONSTRAINT fk_stock_movement_warehouse FOREIGN KEY (tenant_id, company_id, warehouse_id)
        REFERENCES org.warehouse (tenant_id, company_id, warehouse_id),
    CONSTRAINT fk_stock_movement_counterpart_warehouse
        FOREIGN KEY (tenant_id, company_id, counterpart_warehouse_id)
        REFERENCES org.warehouse (tenant_id, company_id, warehouse_id),
    CONSTRAINT fk_stock_movement_reversal
        FOREIGN KEY (tenant_id, company_id, reversal_of_movement_id)
        REFERENCES inventory.stock_movement (tenant_id, company_id, movement_id),
    CONSTRAINT uq_stock_movement_source_result UNIQUE
        (tenant_id, company_id, source_type, source_event_id, source_line_id,
         source_version, posting_purpose, movement_kind, warehouse_id),
    CONSTRAINT uq_stock_movement_position UNIQUE
        (tenant_id, company_id, item_id, warehouse_id, effective_date, sequence_key),
    CONSTRAINT ck_stock_movement_kind_quantity CHECK
        (movement_kind IN (1,2,3,4,5) AND base_quantity <> 0
         AND (movement_kind NOT IN (1,4) OR base_quantity > 0)
         AND (movement_kind NOT IN (2,3) OR base_quantity < 0)),
    CONSTRAINT ck_stock_movement_uom CHECK
        (base_uom_code = upper(btrim(base_uom_code)) AND base_uom_code ~ '^[A-Z0-9][A-Z0-9-]{0,15}$'),
    CONSTRAINT ck_stock_movement_source CHECK
        (source_version > 0 AND sequence_key > 0
         AND source_type = btrim(source_type) AND source_type <> ''
         AND posting_purpose = btrim(posting_purpose) AND posting_purpose <> ''),
    CONSTRAINT ck_stock_movement_transfer_context CHECK
        ((movement_kind IN (3,4) AND transfer_id IS NOT NULL
          AND counterpart_warehouse_id IS NOT NULL AND counterpart_warehouse_id <> warehouse_id)
         OR
         (movement_kind NOT IN (3,4) AND transfer_id IS NULL AND counterpart_warehouse_id IS NULL)),
    CONSTRAINT ck_stock_movement_reversal_reference CHECK
        (reversal_of_movement_id IS NULL OR reversal_of_movement_id <> movement_id)
);

CREATE INDEX ix_inventory_item_company_scope_active
    ON inventory.item_company (tenant_id, company_id, is_active, item_id);
CREATE INDEX ix_user_warehouse_scope_profile_validity
    ON iam.user_warehouse_scope (user_profile_id, tenant_id, company_id, valid_from, valid_to);
CREATE INDEX ix_stock_movement_scope_item_position
    ON inventory.stock_movement (tenant_id, company_id, item_id, warehouse_id, effective_date, sequence_key);
CREATE INDEX ix_stock_movement_scope_transfer
    ON inventory.stock_movement (tenant_id, company_id, transfer_id)
    WHERE transfer_id IS NOT NULL;
CREATE UNIQUE INDEX uq_stock_movement_single_reversal
    ON inventory.stock_movement (tenant_id, company_id, reversal_of_movement_id)
    WHERE reversal_of_movement_id IS NOT NULL;

CREATE FUNCTION org.guard_warehouse_identity_update() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, org
AS $$
BEGIN
    IF NEW.tenant_id <> OLD.tenant_id OR NEW.company_id <> OLD.company_id
       OR NEW.warehouse_id <> OLD.warehouse_id OR NEW.code <> OLD.code
       OR NEW.version <> OLD.version + 1 THEN
        RAISE EXCEPTION 'Warehouse identity/code is immutable and version must increment by one.'
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_warehouse_identity_update
BEFORE UPDATE ON org.warehouse
FOR EACH ROW EXECUTE FUNCTION org.guard_warehouse_identity_update();

CREATE FUNCTION inventory.guard_item_identity_update() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, inventory
AS $$
BEGIN
    IF NEW.tenant_id <> OLD.tenant_id OR NEW.item_id <> OLD.item_id OR NEW.code <> OLD.code
       OR NEW.version <> OLD.version + 1 THEN
        RAISE EXCEPTION 'Item identity/code is immutable and version must increment by one.'
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_inventory_item_identity_update
BEFORE UPDATE ON inventory.item
FOR EACH ROW EXECUTE FUNCTION inventory.guard_item_identity_update();

CREATE FUNCTION inventory.guard_item_company_identity_update() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, inventory
AS $$
BEGIN
    IF NEW.tenant_id <> OLD.tenant_id OR NEW.company_id <> OLD.company_id
       OR NEW.item_id <> OLD.item_id OR NEW.version <> OLD.version + 1 THEN
        RAISE EXCEPTION 'Item company scope is immutable and version must increment by one.'
            USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_inventory_item_company_identity_update
BEFORE UPDATE ON inventory.item_company
FOR EACH ROW EXECUTE FUNCTION inventory.guard_item_company_identity_update();

CREATE FUNCTION inventory.assert_immediate_transfer_balanced() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, inventory
AS $$
DECLARE
    movement_count integer;
    issue_row inventory.stock_movement%ROWTYPE;
    receipt_row inventory.stock_movement%ROWTYPE;
BEGIN
    IF NEW.transfer_id IS NULL THEN
        RETURN NULL;
    END IF;

    SELECT count(*) INTO movement_count
    FROM inventory.stock_movement
    WHERE tenant_id = NEW.tenant_id AND company_id = NEW.company_id AND transfer_id = NEW.transfer_id;

    IF movement_count <> 2 THEN
        RAISE EXCEPTION 'Immediate transfer must contain exactly two movements.' USING ERRCODE = '23514';
    END IF;

    SELECT * INTO issue_row
    FROM inventory.stock_movement
    WHERE tenant_id = NEW.tenant_id AND company_id = NEW.company_id
      AND transfer_id = NEW.transfer_id AND movement_kind = 3;
    SELECT * INTO receipt_row
    FROM inventory.stock_movement
    WHERE tenant_id = NEW.tenant_id AND company_id = NEW.company_id
      AND transfer_id = NEW.transfer_id AND movement_kind = 4;

    IF issue_row.movement_id IS NULL OR receipt_row.movement_id IS NULL
       OR issue_row.base_quantity + receipt_row.base_quantity <> 0
       OR issue_row.item_id <> receipt_row.item_id
       OR issue_row.base_uom_code <> receipt_row.base_uom_code
       OR issue_row.effective_date <> receipt_row.effective_date
       OR issue_row.source_type <> receipt_row.source_type
       OR issue_row.source_event_id <> receipt_row.source_event_id
       OR issue_row.source_line_id <> receipt_row.source_line_id
       OR issue_row.source_version <> receipt_row.source_version
       OR issue_row.posting_purpose <> receipt_row.posting_purpose
       OR issue_row.counterpart_warehouse_id <> receipt_row.warehouse_id
       OR receipt_row.counterpart_warehouse_id <> issue_row.warehouse_id THEN
        RAISE EXCEPTION 'Immediate transfer movements do not conserve exact source, context and quantity.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER trg_stock_movement_transfer_balance
AFTER INSERT ON inventory.stock_movement
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION inventory.assert_immediate_transfer_balanced();

CREATE FUNCTION inventory.assert_stock_movement_reversal() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, inventory
AS $$
DECLARE
    original inventory.stock_movement%ROWTYPE;
BEGIN
    IF NEW.reversal_of_movement_id IS NULL THEN
        RETURN NULL;
    END IF;

    SELECT * INTO original
    FROM inventory.stock_movement
    WHERE tenant_id=NEW.tenant_id AND company_id=NEW.company_id
      AND movement_id=NEW.reversal_of_movement_id;

    IF original.movement_id IS NULL
       OR NEW.item_id <> original.item_id
       OR NEW.warehouse_id <> original.warehouse_id
       OR NEW.base_uom_code <> original.base_uom_code
       OR NEW.base_quantity + original.base_quantity <> 0
       OR NEW.recorded_at < original.recorded_at THEN
        RAISE EXCEPTION 'Stock movement reversal must exactly counter its original movement.'
            USING ERRCODE = '23514';
    END IF;

    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER trg_stock_movement_reversal
AFTER INSERT ON inventory.stock_movement
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION inventory.assert_stock_movement_reversal();

ALTER TABLE org.warehouse OWNER TO kagu_erp_schema_owner;
ALTER TABLE iam.user_warehouse_scope OWNER TO kagu_erp_schema_owner;
ALTER TABLE inventory.item OWNER TO kagu_erp_schema_owner;
ALTER TABLE inventory.item_company OWNER TO kagu_erp_schema_owner;
ALTER TABLE inventory.stock_movement OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION org.guard_warehouse_identity_update() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.guard_item_identity_update() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.guard_item_company_identity_update() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.assert_immediate_transfer_balanced() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.assert_stock_movement_reversal() OWNER TO kagu_erp_schema_owner;

REVOKE ALL ON TABLE org.warehouse, iam.user_warehouse_scope, inventory.item, inventory.item_company, inventory.stock_movement FROM PUBLIC;
GRANT SELECT ON TABLE iam.user_warehouse_scope TO kagu_erp_app;
GRANT SELECT, INSERT, UPDATE ON TABLE org.warehouse, inventory.item, inventory.item_company TO kagu_erp_app;
GRANT SELECT, INSERT ON TABLE inventory.stock_movement TO kagu_erp_app;

ALTER TABLE org.warehouse ENABLE ROW LEVEL SECURITY;
ALTER TABLE org.warehouse FORCE ROW LEVEL SECURITY;
ALTER TABLE iam.user_warehouse_scope ENABLE ROW LEVEL SECURITY;
ALTER TABLE iam.user_warehouse_scope FORCE ROW LEVEL SECURITY;
ALTER TABLE inventory.item ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.item FORCE ROW LEVEL SECURITY;
ALTER TABLE inventory.item_company ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.item_company FORCE ROW LEVEL SECURITY;
ALTER TABLE inventory.stock_movement ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.stock_movement FORCE ROW LEVEL SECURITY;

CREATE POLICY warehouse_scope_policy ON org.warehouse FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY warehouse_owner_policy ON org.warehouse FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

CREATE POLICY user_warehouse_scope_actor_policy ON iam.user_warehouse_scope FOR SELECT TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[]))
    AND user_profile_id = nullif(current_setting('app.actor_id', true), '')::uuid);
CREATE POLICY user_warehouse_scope_owner_policy ON iam.user_warehouse_scope FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

CREATE POLICY inventory_item_scope_policy ON inventory.item FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY inventory_item_owner_policy ON inventory.item FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

CREATE POLICY inventory_item_company_scope_policy ON inventory.item_company FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY inventory_item_company_owner_policy ON inventory.item_company FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

CREATE POLICY stock_movement_scope_policy ON inventory.stock_movement FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY stock_movement_owner_policy ON inventory.stock_movement FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

ALTER DEFAULT PRIVILEGES FOR ROLE kagu_erp_schema_owner IN SCHEMA inventory
    REVOKE ALL ON TABLES FROM PUBLIC;
