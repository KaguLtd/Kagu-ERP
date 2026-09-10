-- Expand only. Legacy cancellations are NOT backfilled with an invented effective date.
CREATE TABLE sales.stock_order_cancellation_receipt
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    order_id uuid NOT NULL,
    correlation_id uuid NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL DEFAULT clock_timestamp(),
    PRIMARY KEY (tenant_id,company_id,order_id,correlation_id),
    FOREIGN KEY (tenant_id,company_id,order_id,correlation_id)
        REFERENCES sales.sales_order_transition_event (tenant_id,company_id,order_id,correlation_id)
);

CREATE FUNCTION sales.guard_stock_cancellation_receipt() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog, sales
AS $$
BEGIN
    IF TG_OP <> 'INSERT' THEN
        RAISE EXCEPTION 'Stock cancellation receipts are immutable.' USING ERRCODE='55000';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM sales.sales_order_transition_event e
        WHERE e.tenant_id=NEW.tenant_id AND e.company_id=NEW.company_id
          AND e.order_id=NEW.order_id AND e.correlation_id=NEW.correlation_id AND e.transition=10) THEN
        RAISE EXCEPTION 'Receipt requires the matching cancellation event.' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_stock_cancellation_receipt_guard
BEFORE INSERT OR UPDATE OR DELETE ON sales.stock_order_cancellation_receipt
FOR EACH ROW EXECUTE FUNCTION sales.guard_stock_cancellation_receipt();

ALTER TABLE sales.stock_order_cancellation_receipt OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION sales.guard_stock_cancellation_receipt() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE sales.stock_order_cancellation_receipt FROM PUBLIC,kagu_erp_app;
GRANT SELECT ON TABLE sales.stock_order_cancellation_receipt TO kagu_erp_app;
ALTER TABLE sales.stock_order_cancellation_receipt ENABLE ROW LEVEL SECURITY;
ALTER TABLE sales.stock_order_cancellation_receipt FORCE ROW LEVEL SECURITY;
CREATE POLICY stock_cancellation_receipt_scope ON sales.stock_order_cancellation_receipt FOR ALL TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])))
WITH CHECK (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY stock_cancellation_receipt_owner ON sales.stock_order_cancellation_receipt FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);
