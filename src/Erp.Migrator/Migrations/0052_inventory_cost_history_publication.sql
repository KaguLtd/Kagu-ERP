-- Validated valuation-producer output, NOT purchase invoices or stock/GL postings.
CREATE TABLE inventory.cost_history_publication
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    publication_id uuid NOT NULL CHECK (publication_id <> '00000000-0000-0000-0000-000000000000'::uuid),
    item_id uuid NOT NULL,
    warehouse_id uuid NOT NULL,
    base_uom_code varchar(16) NOT NULL,
    currency_code varchar(3) NOT NULL CHECK (currency_code ~ '^[A-Z]{3}$'),
    effective_date date NOT NULL CHECK (isfinite(effective_date)),
    sequence_key bigint NOT NULL CHECK (sequence_key > 0),
    projection_generation bigint NOT NULL CHECK (projection_generation > 0),
    recorded_cutoff timestamptz NOT NULL CHECK (isfinite(recorded_cutoff)),
    source_checksum varchar(64) NOT NULL CHECK (source_checksum ~ '^[0-9a-f]{64}$'),
    origin smallint NOT NULL CHECK (origin IN (1,2)),
    cost_snapshot_id uuid NULL,
    unit_cost numeric NOT NULL,
    published_at timestamptz NOT NULL DEFAULT clock_timestamp() CHECK (isfinite(published_at)),
    published_by uuid NOT NULL,
    PRIMARY KEY (tenant_id,company_id,publication_id),
    UNIQUE (tenant_id,company_id,item_id,warehouse_id,base_uom_code,currency_code,
        effective_date,sequence_key,projection_generation,recorded_cutoff),
    FOREIGN KEY (tenant_id,company_id,item_id) REFERENCES inventory.item_company (tenant_id,company_id,item_id),
    FOREIGN KEY (tenant_id,item_id,base_uom_code) REFERENCES inventory.item (tenant_id,item_id,base_uom_code),
    FOREIGN KEY (tenant_id,company_id,warehouse_id) REFERENCES org.warehouse (tenant_id,company_id,warehouse_id),
    FOREIGN KEY (tenant_id,published_by) REFERENCES iam.user_profile (tenant_id,id),
    CHECK (recorded_cutoff <= published_at),
    CHECK ((origin=1 AND cost_snapshot_id IS NOT NULL AND cost_snapshot_id <> '00000000-0000-0000-0000-000000000000'::uuid)
        OR (origin=2 AND cost_snapshot_id IS NULL AND unit_cost=0)),
    -- Exact System.Decimal: unsigned 96-bit coefficient and scale 0..28. No implicit rounding.
    CHECK (unit_cost >= 0 AND unit_cost <= 79228162514264337593543950335
        AND scale(unit_cost) BETWEEN 0 AND 28
        AND unit_cost * power(10::numeric,scale(unit_cost)) <= 79228162514264337593543950335)
);
CREATE FUNCTION inventory.guard_cost_history_publication_immutable() RETURNS trigger
LANGUAGE plpgsql SET search_path = pg_catalog,inventory AS $$
BEGIN
    RAISE EXCEPTION 'Published cost history is immutable.' USING ERRCODE='55000';
END;
$$;
CREATE TRIGGER trg_cost_history_publication_immutable
BEFORE UPDATE OR DELETE ON inventory.cost_history_publication
FOR EACH ROW EXECUTE FUNCTION inventory.guard_cost_history_publication_immutable();
ALTER TABLE inventory.cost_history_publication OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION inventory.guard_cost_history_publication_immutable() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE inventory.cost_history_publication FROM PUBLIC,kagu_erp_app;
GRANT SELECT ON TABLE inventory.cost_history_publication TO kagu_erp_app;
ALTER TABLE inventory.cost_history_publication ENABLE ROW LEVEL SECURITY;
ALTER TABLE inventory.cost_history_publication FORCE ROW LEVEL SECURITY;
CREATE POLICY cost_history_publication_scope ON inventory.cost_history_publication FOR SELECT TO kagu_erp_app
USING (tenant_id=nullif(current_setting('app.tenant_id',true),'')::uuid
    AND company_id=ANY(coalesce(nullif(current_setting('app.company_ids',true),'')::uuid[],ARRAY[]::uuid[])));
CREATE POLICY cost_history_publication_owner ON inventory.cost_history_publication
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
