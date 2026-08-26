CREATE FUNCTION accounting.enforce_posted_journal_balance()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, accounting
AS $$
DECLARE
    target_journal_id uuid := coalesce(NEW.journal_id, OLD.journal_id);
    header accounting.posted_journal%ROWTYPE;
    actual_line_count bigint;
    actual_debit numeric(20,4);
    actual_credit numeric(20,4);
BEGIN
    SELECT * INTO header
    FROM accounting.posted_journal
    WHERE journal_id = target_journal_id;

    IF NOT FOUND THEN
        RETURN NULL;
    END IF;

    SELECT count(*), coalesce(sum(debit), 0), coalesce(sum(credit), 0)
      INTO actual_line_count, actual_debit, actual_credit
    FROM accounting.posted_journal_line
    WHERE journal_id = target_journal_id;

    IF actual_line_count <> header.line_count
       OR actual_debit <> header.total_debit
       OR actual_credit <> header.total_credit
       OR actual_debit <> actual_credit THEN
        RAISE EXCEPTION 'Posted journal lines do not cross-foot to the immutable header.'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_posted_journal_cross_foot';
    END IF;

    RETURN NULL;
END;
$$;

ALTER FUNCTION accounting.enforce_posted_journal_balance() OWNER TO kagu_erp_schema_owner;
REVOKE ALL ON FUNCTION accounting.enforce_posted_journal_balance() FROM PUBLIC;

CREATE CONSTRAINT TRIGGER posted_journal_header_balance_guard
AFTER INSERT OR UPDATE ON accounting.posted_journal
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION accounting.enforce_posted_journal_balance();

CREATE CONSTRAINT TRIGGER posted_journal_line_balance_guard
AFTER INSERT OR UPDATE OR DELETE ON accounting.posted_journal_line
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION accounting.enforce_posted_journal_balance();
