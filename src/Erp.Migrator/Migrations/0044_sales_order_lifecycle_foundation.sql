CREATE SCHEMA IF NOT EXISTS sales AUTHORIZATION kagu_erp_schema_owner;
REVOKE ALL ON SCHEMA sales FROM PUBLIC;
GRANT USAGE ON SCHEMA sales TO kagu_erp_app;

CREATE TABLE sales.sales_order
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    order_id uuid NOT NULL,
    maker_id uuid NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    status smallint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    created_by uuid NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    updated_by uuid NOT NULL,
    CONSTRAINT pk_sales_order PRIMARY KEY (tenant_id, company_id, order_id),
    CONSTRAINT fk_sales_order_company FOREIGN KEY (tenant_id, company_id)
        REFERENCES org.company (tenant_id, id),
    CONSTRAINT ck_sales_order_version CHECK (version > 0),
    CONSTRAINT ck_sales_order_status CHECK (status BETWEEN 1 AND 9)
);

CREATE TABLE sales.sales_order_transition_event
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    event_id uuid NOT NULL,
    order_id uuid NOT NULL,
    previous_version bigint NOT NULL,
    new_version bigint NOT NULL,
    previous_status smallint NOT NULL,
    new_status smallint NOT NULL,
    transition smallint NOT NULL,
    previous_maker_id uuid NOT NULL,
    new_maker_id uuid NOT NULL,
    actor_id uuid NOT NULL,
    correlation_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL,
    reason varchar(500),
    CONSTRAINT pk_sales_order_transition_event
        PRIMARY KEY (tenant_id, company_id, event_id),
    CONSTRAINT fk_sales_order_transition_order
        FOREIGN KEY (tenant_id, company_id, order_id)
        REFERENCES sales.sales_order (tenant_id, company_id, order_id),
    CONSTRAINT uq_sales_order_transition_version
        UNIQUE (tenant_id, company_id, order_id, new_version),
    CONSTRAINT uq_sales_order_transition_correlation
        UNIQUE (tenant_id, company_id, order_id, correlation_id),
    CONSTRAINT ck_sales_order_transition_version
        CHECK (previous_version > 0 AND new_version = previous_version + 1),
    CONSTRAINT ck_sales_order_transition_status
        CHECK (previous_status BETWEEN 1 AND 9 AND new_status BETWEEN 1 AND 9
               AND transition BETWEEN 1 AND 10),
    CONSTRAINT ck_sales_order_transition_reason
        CHECK (reason IS NULL OR (reason=btrim(reason) AND reason <> '')),
    CONSTRAINT ck_sales_order_transition_required_reason
        CHECK (transition NOT IN (3,5,10) OR reason IS NOT NULL)
);

CREATE INDEX ix_sales_order_scope_status
    ON sales.sales_order (tenant_id, company_id, status, order_id);
CREATE INDEX ix_sales_order_transition_timeline
    ON sales.sales_order_transition_event
       (tenant_id, company_id, order_id, new_version);

CREATE FUNCTION sales.guard_sales_order_update() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, sales
AS $$
BEGIN
    IF NEW.tenant_id <> OLD.tenant_id OR NEW.company_id <> OLD.company_id
       OR NEW.order_id <> OLD.order_id OR NEW.created_at <> OLD.created_at
       OR NEW.created_by <> OLD.created_by OR NEW.version <> OLD.version + 1 THEN
        RAISE EXCEPTION 'Sales order identity is immutable and version must increment by one.'
            USING ERRCODE='23514';
    END IF;

    IF NOT ((OLD.status=1 AND NEW.status IN (2,9))
        OR (OLD.status=2 AND NEW.status IN (1,3,8))
        OR (OLD.status=8 AND NEW.status=1)
        OR (OLD.status=3 AND NEW.status IN (4,9))
        OR (OLD.status=4 AND NEW.status IN (5,6,9))
        OR (OLD.status=5 AND NEW.status IN (5,6))
        OR (OLD.status=6 AND NEW.status=7)) THEN
        RAISE EXCEPTION 'Sales order status transition is invalid.' USING ERRCODE='23514';
    END IF;

    IF (OLD.status=1 AND NEW.status=2 AND NEW.maker_id <> NEW.updated_by)
       OR (NOT (OLD.status=1 AND NEW.status=2) AND NEW.maker_id <> OLD.maker_id) THEN
        RAISE EXCEPTION 'Sales order maker can change only to the submit actor.' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_sales_order_update
BEFORE UPDATE ON sales.sales_order
FOR EACH ROW EXECUTE FUNCTION sales.guard_sales_order_update();

CREATE FUNCTION sales.assert_sales_order_transition_event() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, sales
AS $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM sales.sales_order_transition_event event
        WHERE event.tenant_id=NEW.tenant_id AND event.company_id=NEW.company_id
          AND event.order_id=NEW.order_id AND event.previous_version=OLD.version
          AND event.new_version=NEW.version AND event.previous_status=OLD.status
          AND event.new_status=NEW.status AND event.previous_maker_id=OLD.maker_id
          AND event.new_maker_id=NEW.maker_id AND event.actor_id=NEW.updated_by
          AND (event.transition NOT IN (2,3) OR event.actor_id<>event.previous_maker_id)
          AND ((event.previous_status=1 AND event.new_status=2 AND event.transition=1)
            OR (event.previous_status=1 AND event.new_status=9 AND event.transition=10)
            OR (event.previous_status=2 AND event.new_status=3 AND event.transition=2)
            OR (event.previous_status=2 AND event.new_status=8 AND event.transition=3)
            OR (event.previous_status=2 AND event.new_status=1 AND event.transition=4)
            OR (event.previous_status=8 AND event.new_status=1 AND event.transition=5)
            OR (event.previous_status=3 AND event.new_status=4 AND event.transition=6)
            OR (event.previous_status=3 AND event.new_status=9 AND event.transition=10)
            OR (event.previous_status=4 AND event.new_status=5 AND event.transition=7)
            OR (event.previous_status=4 AND event.new_status=6 AND event.transition=8)
            OR (event.previous_status=4 AND event.new_status=9 AND event.transition=10)
            OR (event.previous_status=5 AND event.new_status=5 AND event.transition=7)
            OR (event.previous_status=5 AND event.new_status=6 AND event.transition=8)
            OR (event.previous_status=6 AND event.new_status=7 AND event.transition=9))
    ) THEN
        RAISE EXCEPTION 'Sales order state update requires its exact append-only transition event.'
            USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER trg_sales_order_transition_event_required
AFTER UPDATE ON sales.sales_order
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION sales.assert_sales_order_transition_event();

CREATE FUNCTION sales.assert_sales_order_transition_applied() RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, sales
AS $$
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM sales.sales_order order_state
        WHERE order_state.tenant_id=NEW.tenant_id AND order_state.company_id=NEW.company_id
          AND order_state.order_id=NEW.order_id AND order_state.version>=NEW.new_version
          AND (order_state.version<>NEW.new_version
               OR (order_state.status=NEW.new_status AND order_state.maker_id=NEW.new_maker_id))
    ) THEN
        RAISE EXCEPTION 'Sales order transition event must be applied to its current projection.'
            USING ERRCODE='23514';
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER trg_sales_order_transition_applied
AFTER INSERT ON sales.sales_order_transition_event
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION sales.assert_sales_order_transition_applied();

ALTER TABLE sales.sales_order OWNER TO kagu_erp_schema_owner;
ALTER TABLE sales.sales_order_transition_event OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION sales.guard_sales_order_update() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION sales.assert_sales_order_transition_event() OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION sales.assert_sales_order_transition_applied() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE sales.sales_order, sales.sales_order_transition_event FROM PUBLIC;
GRANT SELECT,INSERT,UPDATE ON TABLE sales.sales_order TO kagu_erp_app;
GRANT SELECT,INSERT ON TABLE sales.sales_order_transition_event TO kagu_erp_app;

ALTER TABLE sales.sales_order ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales.sales_order FORCE ROW LEVEL SECURITY;
ALTER TABLE sales.sales_order_transition_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales.sales_order_transition_event FORCE ROW LEVEL SECURITY;

CREATE POLICY sales_order_scope_policy ON sales.sales_order FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY sales_order_owner_policy ON sales.sales_order FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

CREATE POLICY sales_order_transition_scope_policy ON sales.sales_order_transition_event FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY sales_order_transition_owner_policy ON sales.sales_order_transition_event FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);

ALTER DEFAULT PRIVILEGES FOR ROLE kagu_erp_schema_owner IN SCHEMA sales
    REVOKE ALL ON TABLES FROM PUBLIC;
