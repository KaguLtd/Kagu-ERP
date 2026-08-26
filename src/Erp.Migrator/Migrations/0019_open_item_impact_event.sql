CREATE TABLE party.open_item_impact_event
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    event_id uuid NOT NULL,
    party_account_id uuid NOT NULL,
    due_schedule_line_id uuid NOT NULL,
    payment_id uuid NULL,
    currency char(3) NOT NULL,
    impact_kind smallint NOT NULL,
    amount numeric(20,4) NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    reverses_event_id uuid NULL,
    CONSTRAINT pk_open_item_impact_event PRIMARY KEY (tenant_id, company_id, event_id),
    CONSTRAINT uq_open_item_impact_reversal UNIQUE (tenant_id, company_id, reverses_event_id),
    CONSTRAINT fk_open_item_impact_due_line FOREIGN KEY (tenant_id, company_id, due_schedule_line_id)
        REFERENCES party.due_schedule_line (tenant_id, company_id, due_schedule_line_id),
    CONSTRAINT fk_open_item_impact_reversal FOREIGN KEY (tenant_id, company_id, reverses_event_id)
        REFERENCES party.open_item_impact_event (tenant_id, company_id, event_id),
    CONSTRAINT ck_open_item_impact_currency CHECK (currency ~ '^[A-Z]{3}$'),
    CONSTRAINT ck_open_item_impact_kind CHECK (impact_kind BETWEEN 1 AND 4),
    CONSTRAINT ck_open_item_impact_amount CHECK (amount > 0),
    CONSTRAINT ck_open_item_impact_payment CHECK
        ((impact_kind IN (1,2) AND payment_id IS NOT NULL) OR
         (impact_kind IN (3,4) AND payment_id IS NULL)),
    CONSTRAINT ck_open_item_impact_reversal_shape CHECK
        ((impact_kind IN (2,4) AND reverses_event_id IS NOT NULL) OR
         (impact_kind IN (1,3) AND reverses_event_id IS NULL))
);

CREATE FUNCTION party.enforce_open_item_exact_counter()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, party
AS $$
DECLARE
    original party.open_item_impact_event%ROWTYPE;
BEGIN
    IF NEW.reverses_event_id IS NULL THEN RETURN NEW; END IF;
    SELECT * INTO original FROM party.open_item_impact_event
    WHERE tenant_id = NEW.tenant_id AND company_id = NEW.company_id
      AND event_id = NEW.reverses_event_id;
    IF NOT FOUND OR original.impact_kind <> (CASE NEW.impact_kind WHEN 2 THEN 1 WHEN 4 THEN 3 ELSE 0 END)
       OR original.party_account_id <> NEW.party_account_id
       OR original.due_schedule_line_id <> NEW.due_schedule_line_id
       OR original.currency <> NEW.currency OR original.amount <> NEW.amount
       OR original.payment_id IS DISTINCT FROM NEW.payment_id
       OR NEW.effective_date < original.effective_date
       OR NEW.recorded_at < original.recorded_at THEN
        RAISE EXCEPTION 'Open-item counter event does not exactly reverse its original.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_open_item_exact_counter';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER open_item_exact_counter_guard
BEFORE INSERT ON party.open_item_impact_event
FOR EACH ROW EXECUTE FUNCTION party.enforce_open_item_exact_counter();

ALTER TABLE party.open_item_impact_event OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION party.enforce_open_item_exact_counter() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE party.open_item_impact_event FROM PUBLIC;
REVOKE ALL ON FUNCTION party.enforce_open_item_exact_counter() FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE party.open_item_impact_event TO kagu_erp_app;

ALTER TABLE party.open_item_impact_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE party.open_item_impact_event FORCE ROW LEVEL SECURITY;
CREATE POLICY open_item_impact_scope_policy ON party.open_item_impact_event FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY open_item_impact_owner_policy ON party.open_item_impact_event FOR ALL TO kagu_erp_schema_owner
USING (true) WITH CHECK (true);
