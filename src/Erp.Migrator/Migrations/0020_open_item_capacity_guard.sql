CREATE FUNCTION party.enforce_open_item_capacity()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, party
AS $$
DECLARE
    due_original_amount numeric(20,4);
    allocated_amount numeric(20,4);
    written_off_amount numeric(20,4);
BEGIN
    SELECT original_amount INTO due_original_amount
    FROM party.due_schedule_line
    WHERE tenant_id = NEW.tenant_id AND company_id = NEW.company_id
      AND due_schedule_line_id = NEW.due_schedule_line_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Open-item due-schedule line does not exist.'
            USING ERRCODE = '23503', CONSTRAINT = 'fk_open_item_impact_due_line';
    END IF;

    SELECT
        coalesce(sum(CASE impact_kind WHEN 1 THEN amount WHEN 2 THEN -amount ELSE 0 END), 0),
        coalesce(sum(CASE impact_kind WHEN 3 THEN amount WHEN 4 THEN -amount ELSE 0 END), 0)
    INTO allocated_amount, written_off_amount
    FROM party.open_item_impact_event
    WHERE tenant_id = NEW.tenant_id AND company_id = NEW.company_id
      AND due_schedule_line_id = NEW.due_schedule_line_id;

    IF allocated_amount < 0 OR written_off_amount < 0
       OR allocated_amount + written_off_amount > due_original_amount THEN
        RAISE EXCEPTION 'Open-item impacts exceed the immutable due-line capacity.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_open_item_impact_capacity';
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER open_item_capacity_guard
AFTER INSERT ON party.open_item_impact_event
DEFERRABLE INITIALLY IMMEDIATE
FOR EACH ROW EXECUTE FUNCTION party.enforce_open_item_capacity();

ALTER FUNCTION party.enforce_open_item_capacity() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON FUNCTION party.enforce_open_item_capacity() FROM PUBLIC;
