CREATE TABLE sales.sales_order_line
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    order_id uuid NOT NULL,
    order_line_id uuid NOT NULL,
    item_id uuid NOT NULL,
    base_uom_code varchar(16) NOT NULL,
    ordered_base_quantity numeric(20,6) NOT NULL,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    CONSTRAINT pk_sales_order_line
        PRIMARY KEY (tenant_id, company_id, order_id, order_line_id),
    CONSTRAINT fk_sales_order_line_order
        FOREIGN KEY (tenant_id, company_id, order_id)
        REFERENCES sales.sales_order (tenant_id, company_id, order_id),
    CONSTRAINT fk_sales_order_line_item_company
        FOREIGN KEY (tenant_id, company_id, item_id)
        REFERENCES inventory.item_company (tenant_id, company_id, item_id),
    CONSTRAINT fk_sales_order_line_item_uom
        FOREIGN KEY (tenant_id, item_id, base_uom_code)
        REFERENCES inventory.item (tenant_id, item_id, base_uom_code),
    CONSTRAINT uq_sales_order_line_reservation_source
        UNIQUE (tenant_id, company_id, order_id, order_line_id, item_id, base_uom_code),
    CONSTRAINT ck_sales_order_line_quantity
        CHECK (ordered_base_quantity > 0),
    CONSTRAINT ck_sales_order_line_uom
        CHECK (base_uom_code = upper(btrim(base_uom_code))
               AND base_uom_code ~ '^[A-Z0-9][A-Z0-9-]{0,15}$')
);

CREATE INDEX ix_sales_order_line_scope_item
    ON sales.sales_order_line (tenant_id, company_id, item_id, order_id, order_line_id);

CREATE FUNCTION sales.assert_sales_order_has_lines() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, sales
AS $$
BEGIN
    IF (TG_OP = 'INSERT' OR (NEW.status = 4 AND OLD.status <> 4)) AND NOT EXISTS
    (
        SELECT 1
        FROM sales.sales_order_line line
        WHERE line.tenant_id = NEW.tenant_id
          AND line.company_id = NEW.company_id
          AND line.order_id = NEW.order_id
    ) THEN
        RAISE EXCEPTION 'Sales order requires at least one authoritative line.'
            USING ERRCODE = '23514';
    END IF;
    RETURN NULL;
END
$$;

CREATE CONSTRAINT TRIGGER trg_sales_order_has_lines
AFTER INSERT OR UPDATE ON sales.sales_order
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION sales.assert_sales_order_has_lines();

ALTER TABLE sales.sales_order_line OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION sales.assert_sales_order_has_lines() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE sales.sales_order_line FROM PUBLIC;
GRANT SELECT,INSERT ON TABLE sales.sales_order_line TO kagu_erp_app;

ALTER TABLE sales.sales_order_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales.sales_order_line FORCE ROW LEVEL SECURITY;

CREATE POLICY sales_order_line_scope_policy ON sales.sales_order_line FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));

CREATE POLICY sales_order_line_owner_policy ON sales.sales_order_line FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

ALTER DEFAULT PRIVILEGES FOR ROLE kagu_erp_schema_owner IN SCHEMA sales
    REVOKE ALL ON TABLES FROM PUBLIC;
