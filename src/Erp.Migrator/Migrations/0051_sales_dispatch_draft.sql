-- Immutable preparation snapshots, NOT posted dispatches or fulfilment allocations.
CREATE TABLE sales.dispatch_draft
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    dispatch_id uuid NOT NULL,
    order_id uuid NOT NULL,
    order_version bigint NOT NULL CHECK (order_version > 0),
    effective_date date NOT NULL,
    request_fingerprint varchar(64) NOT NULL CHECK (request_fingerprint ~ '^[0-9a-f]{64}$'),
    line_count integer NOT NULL CHECK (line_count BETWEEN 1 AND 500),
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    PRIMARY KEY (tenant_id,company_id,dispatch_id),
    UNIQUE (tenant_id,company_id,dispatch_id,order_id),
    FOREIGN KEY (tenant_id,company_id,order_id) REFERENCES sales.sales_order (tenant_id,company_id,order_id),
    FOREIGN KEY (tenant_id,created_by) REFERENCES iam.user_profile (tenant_id,id),
    CHECK (dispatch_id <> '00000000-0000-0000-0000-000000000000'::uuid)
);
CREATE TABLE sales.dispatch_draft_line
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    dispatch_id uuid NOT NULL,
    order_id uuid NOT NULL,
    order_line_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    item_id uuid NOT NULL,
    base_uom_code varchar(16) NOT NULL,
    base_quantity numeric(20,6) NOT NULL CHECK (base_quantity > 0),
    PRIMARY KEY (tenant_id,company_id,dispatch_id,order_line_id),
    FOREIGN KEY (tenant_id,company_id,dispatch_id,order_id)
        REFERENCES sales.dispatch_draft (tenant_id,company_id,dispatch_id,order_id),
    FOREIGN KEY (tenant_id,company_id,order_id,order_line_id,item_id,base_uom_code)
        REFERENCES sales.sales_order_line (tenant_id,company_id,order_id,order_line_id,item_id,base_uom_code),
    FOREIGN KEY (tenant_id,company_id,warehouse_id) REFERENCES org.warehouse (tenant_id,company_id,warehouse_id)
);
CREATE INDEX ix_dispatch_draft_order ON sales.dispatch_draft (tenant_id,company_id,order_id,recorded_at,dispatch_id);

CREATE FUNCTION sales.guard_dispatch_draft_immutable() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, sales AS $$
BEGIN
    RAISE EXCEPTION 'Dispatch preparation snapshots are immutable.' USING ERRCODE='55000';
END;
$$;
CREATE TRIGGER trg_dispatch_draft_immutable BEFORE UPDATE OR DELETE ON sales.dispatch_draft
FOR EACH ROW EXECUTE FUNCTION sales.guard_dispatch_draft_immutable();
CREATE TRIGGER trg_dispatch_draft_line_immutable BEFORE UPDATE OR DELETE ON sales.dispatch_draft_line
FOR EACH ROW EXECUTE FUNCTION sales.guard_dispatch_draft_immutable();

CREATE FUNCTION sales.assert_dispatch_draft_complete() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, sales AS $$
DECLARE expected integer; actual bigint;
BEGIN
    SELECT line_count INTO expected FROM sales.dispatch_draft
    WHERE tenant_id=NEW.tenant_id AND company_id=NEW.company_id AND dispatch_id=NEW.dispatch_id;
    SELECT count(*) INTO actual FROM sales.dispatch_draft_line
    WHERE tenant_id=NEW.tenant_id AND company_id=NEW.company_id AND dispatch_id=NEW.dispatch_id;
    IF expected IS NULL OR expected <> actual THEN
        RAISE EXCEPTION 'Dispatch draft must contain its exact declared line set.' USING ERRCODE='23514';
    END IF;
    IF EXISTS (SELECT 1 FROM sales.dispatch_draft_line d
        JOIN sales.sales_order_line o ON o.tenant_id=d.tenant_id AND o.company_id=d.company_id
          AND o.order_id=d.order_id AND o.order_line_id=d.order_line_id
        WHERE d.tenant_id=NEW.tenant_id AND d.company_id=NEW.company_id AND d.dispatch_id=NEW.dispatch_id
          AND d.base_quantity>o.ordered_base_quantity) THEN
        RAISE EXCEPTION 'Draft line cannot exceed its source commitment.' USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END;
$$;
CREATE CONSTRAINT TRIGGER trg_dispatch_draft_complete AFTER INSERT ON sales.dispatch_draft
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sales.assert_dispatch_draft_complete();
CREATE CONSTRAINT TRIGGER trg_dispatch_draft_line_complete AFTER INSERT ON sales.dispatch_draft_line
DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION sales.assert_dispatch_draft_complete();

ALTER TABLE sales.dispatch_draft OWNER TO kagu_erp_schema_owner;
ALTER TABLE sales.dispatch_draft_line OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION sales.guard_dispatch_draft_immutable() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION sales.assert_dispatch_draft_complete() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE sales.dispatch_draft,sales.dispatch_draft_line FROM PUBLIC,kagu_erp_app;
GRANT SELECT ON TABLE sales.dispatch_draft,sales.dispatch_draft_line TO kagu_erp_app;
ALTER TABLE sales.dispatch_draft ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales.dispatch_draft FORCE ROW LEVEL SECURITY;
ALTER TABLE sales.dispatch_draft_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales.dispatch_draft_line FORCE ROW LEVEL SECURITY;
CREATE POLICY dispatch_draft_scope ON sales.dispatch_draft FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY dispatch_draft_line_scope ON sales.dispatch_draft_line FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY dispatch_draft_owner ON sales.dispatch_draft FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
CREATE POLICY dispatch_draft_line_owner ON sales.dispatch_draft_line FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
