CREATE TABLE party.open_item_restriction_event
(
    tenant_id uuid NOT NULL,
    company_id uuid NOT NULL,
    event_id uuid NOT NULL,
    party_account_id uuid NOT NULL,
    due_schedule_line_id uuid NOT NULL,
    restriction_kind smallint NOT NULL,
    restriction_action smallint NOT NULL,
    reason_code varchar(60) NOT NULL,
    effective_date date NOT NULL,
    recorded_at timestamptz NOT NULL,
    recorded_by uuid NOT NULL,
    releases_event_id uuid NULL,
    CONSTRAINT pk_open_item_restriction_event PRIMARY KEY (tenant_id, company_id, event_id),
    CONSTRAINT uq_open_item_restriction_release UNIQUE (tenant_id, company_id, releases_event_id),
    CONSTRAINT fk_open_item_restriction_due_line
        FOREIGN KEY (tenant_id, company_id, due_schedule_line_id)
        REFERENCES party.due_schedule_line (tenant_id, company_id, due_schedule_line_id),
    CONSTRAINT fk_open_item_restriction_release
        FOREIGN KEY (tenant_id, company_id, releases_event_id)
        REFERENCES party.open_item_restriction_event (tenant_id, company_id, event_id),
    CONSTRAINT ck_open_item_restriction_kind CHECK (restriction_kind IN (1, 2)),
    CONSTRAINT ck_open_item_restriction_action CHECK (restriction_action IN (1, 2)),
    CONSTRAINT ck_open_item_restriction_reason CHECK
        (reason_code = btrim(reason_code) AND reason_code <> ''),
    CONSTRAINT ck_open_item_restriction_release_shape CHECK
        ((restriction_action = 1 AND releases_event_id IS NULL) OR
         (restriction_action = 2 AND releases_event_id IS NOT NULL))
);

CREATE FUNCTION party.enforce_open_item_restriction_stream()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, party
AS $$
DECLARE
    due_party_account_id uuid;
    original party.open_item_restriction_event%ROWTYPE;
    latest_effective_date date;
    latest_recorded_at timestamptz;
BEGIN
    -- Let INSERT ... ON CONFLICT reach its immutable replay comparison even if
    -- later stream facts already exist.
    IF EXISTS (
        SELECT 1
        FROM party.open_item_restriction_event existing
        WHERE existing.tenant_id = NEW.tenant_id
          AND existing.company_id = NEW.company_id
          AND existing.event_id = NEW.event_id
    ) THEN
        RETURN NEW;
    END IF;

    SELECT party_account_id
      INTO due_party_account_id
    FROM party.due_schedule_line
    WHERE tenant_id = NEW.tenant_id
      AND company_id = NEW.company_id
      AND due_schedule_line_id = NEW.due_schedule_line_id
    FOR UPDATE;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Restriction due-schedule line does not exist.'
            USING ERRCODE = '23503', CONSTRAINT = 'fk_open_item_restriction_due_line';
    END IF;
    IF due_party_account_id <> NEW.party_account_id THEN
        RAISE EXCEPTION 'Restriction PartyAccount does not match its due-schedule line.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_open_item_restriction_due_context';
    END IF;

    SELECT max(effective_date), max(recorded_at)
      INTO latest_effective_date, latest_recorded_at
    FROM party.open_item_restriction_event
    WHERE tenant_id = NEW.tenant_id
      AND company_id = NEW.company_id
      AND due_schedule_line_id = NEW.due_schedule_line_id
      AND restriction_kind = NEW.restriction_kind;
    IF latest_effective_date IS NOT NULL AND
       (NEW.effective_date < latest_effective_date OR NEW.recorded_at < latest_recorded_at) THEN
        RAISE EXCEPTION 'Restriction stream chronology cannot move backwards.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_open_item_restriction_chronology';
    END IF;

    IF NEW.restriction_action = 1 THEN
        IF EXISTS (
            SELECT 1
            FROM party.open_item_restriction_event applied
            WHERE applied.tenant_id = NEW.tenant_id
              AND applied.company_id = NEW.company_id
              AND applied.due_schedule_line_id = NEW.due_schedule_line_id
              AND applied.restriction_kind = NEW.restriction_kind
              AND applied.restriction_action = 1
              AND NOT EXISTS (
                  SELECT 1
                  FROM party.open_item_restriction_event released
                  WHERE released.tenant_id = applied.tenant_id
                    AND released.company_id = applied.company_id
                    AND released.releases_event_id = applied.event_id
                    AND released.restriction_action = 2
              )
        ) THEN
            RAISE EXCEPTION 'An active restriction of this kind already exists.'
                USING ERRCODE = '23514', CONSTRAINT = 'ck_open_item_restriction_single_active';
        END IF;
        RETURN NEW;
    END IF;

    SELECT *
      INTO original
    FROM party.open_item_restriction_event
    WHERE tenant_id = NEW.tenant_id
      AND company_id = NEW.company_id
      AND event_id = NEW.releases_event_id
    FOR UPDATE;
    IF NOT FOUND OR original.restriction_action <> 1
       OR original.restriction_kind <> NEW.restriction_kind
       OR original.party_account_id <> NEW.party_account_id
       OR original.due_schedule_line_id <> NEW.due_schedule_line_id
       OR NEW.effective_date < original.effective_date
       OR NEW.recorded_at < original.recorded_at THEN
        RAISE EXCEPTION 'Restriction release does not exactly match its applied event.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_open_item_restriction_exact_release';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER open_item_restriction_stream_guard
BEFORE INSERT ON party.open_item_restriction_event
FOR EACH ROW EXECUTE FUNCTION party.enforce_open_item_restriction_stream();

CREATE INDEX ix_open_item_restriction_due_cut
    ON party.open_item_restriction_event
       (tenant_id, company_id, due_schedule_line_id, effective_date, recorded_at, event_id);

ALTER TABLE party.open_item_restriction_event OWNER TO kagu_erp_schema_owner;
ALTER FUNCTION party.enforce_open_item_restriction_stream() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON TABLE party.open_item_restriction_event FROM PUBLIC;
REVOKE ALL ON FUNCTION party.enforce_open_item_restriction_stream() FROM PUBLIC;
GRANT SELECT, INSERT ON TABLE party.open_item_restriction_event TO kagu_erp_app;

ALTER TABLE party.open_item_restriction_event ENABLE ROW LEVEL SECURITY;
ALTER TABLE party.open_item_restriction_event FORCE ROW LEVEL SECURITY;
CREATE POLICY open_item_restriction_scope_policy ON party.open_item_restriction_event
FOR ALL TO kagu_erp_app
USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])))
WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid
    AND company_id = ANY(coalesce(nullif(current_setting('app.company_ids', true), '')::uuid[], ARRAY[]::uuid[])));
CREATE POLICY open_item_restriction_owner_policy ON party.open_item_restriction_event
FOR ALL TO kagu_erp_schema_owner USING (true) WITH CHECK (true);
